using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 냄새 자국 남기기 (design/cat-ideas/06, 2026-09-24).
    /// 호스트: 손이나 주머니에 Edible(치즈류)이 있으면 강도 60, 웅덩이를 지나 젖었으면 40, 고양이에게 찍혔으면(05) 20 — 1.5m 걸을 때마다 ScentSystem에 자국.
    /// 웅크리면 간격 2배(덜 남김). 소유 클라에게만 자국 위치를 알려 바닥에 노란 점으로 보여 준다 — 남의 자국은 안 보인다.
    /// </summary>
    public class PlayerScent : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        private PlayerCarryController _carry;
        private Vector3 _lastMarkPos;
        private bool _hasLast;
        private float _nextCatPoll;
        private bool _grudged;
        private float _wetUntil;

        /// <summary>호스트: 웅덩이를 지났다 — 이 시간 동안 젖은 발자국 (design/cat-ideas/06 2단계).</summary>
        public void ServerMarkWet(float seconds) => _wetUntil = Mathf.Max(_wetUntil, Time.time + seconds);
        public bool IsWet => Time.time < _wetUntil;
        private float _coveredUntil;
        /// <summary>호스트: 고양이 침대에 올라가 고양이 냄새로 덮였다 — 이 시간 동안 자국 없음 (design/cat-ideas/06).</summary>
        public void ServerCoverScent(float seconds) => _coveredUntil = Mathf.Max(_coveredUntil, Time.time + seconds);
        public bool IsCovered => Time.time < _coveredUntil;

        // 소유 클라 표시용 풀
        private readonly List<(Transform t, float bornAt, float life)> _dots = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        /// <summary>마지막으로 판단한 자국 강도 (0 = 안 남김). 테스트·HUD용.</summary>
        public float CurrentStrength { get; private set; }
        public int DotsShown { get; private set; }

        private void Awake() => _carry = GetComponent<PlayerCarryController>();

        public override void OnNetworkSpawn()
        {
            if (IsServer && _balance != null) ScentSystem.Configure(_balance);
        }

        private void FixedUpdate()
        {
            if (!IsServer || _balance == null) return;
            CurrentStrength = ComputeStrength();
            if (CurrentStrength <= 0f) { _hasLast = false; return; }

            Vector3 pos = transform.position;
            bool crouching = transform.localScale.y < PlayerController.BaseScaleY * 0.75f;
            float interval = _balance.ScentIntervalMeters * (crouching ? _balance.ScentCrouchIntervalMul : 1f);
            if (_hasLast && (pos - _lastMarkPos).sqrMagnitude < interval * interval) return;
            _lastMarkPos = pos;
            _hasLast = true;
            Vector3 ground = pos + Vector3.down * 0.55f; // 몸 중앙 피벗 → 바닥
            ScentSystem.Add(ground, CurrentStrength, OwnerClientId);
            ScentMarkClientRpc(ground, CurrentStrength / Mathf.Max(0.01f, _balance.ScentDecayPerSec),
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } });
        }

        private float ComputeStrength()
        {
            if (IsCovered) return 0f; // 고양이 냄새로 덮임 — 치즈를 들어도 자국 없음
            if (CarriesEdible()) return _balance.ScentEdible;
            if (IsWet) return _balance.ScentWet; // 젖은 발자국 — 치즈 없이도
            if (Time.time >= _nextCatPoll)
            {
                _nextCatPoll = Time.time + 0.5f;
                _grudged = false;
                foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
                    if (cat.IsSpawned && cat.HasGrudge.Value && cat.GrudgeClientId.Value == OwnerClientId) { _grudged = true; break; }
            }
            return _grudged ? _balance.ScentGrudge : 0f;
        }

        private bool CarriesEdible()
        {
            if (_carry == null) return false;
            if (IsEdible(_carry.CarriedItem)) return true;
            for (int i = 0; i < _carry.SlotCount; i++)
                if (IsEdible(_carry.GetSlotItem(i))) return true;
            return false;
        }

        private static bool IsEdible(CarryableItem item) => item != null && item.Data != null && item.Data.Has(ItemTrait.Edible);

        [ClientRpc]
        private void ScentMarkClientRpc(Vector3 pos, float lifeSeconds, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            if (_balance != null) EventBus.RaiseScentLeft(lifeSeconds * _balance.ScentDecayPerSec); // HUD "냄새 · 치즈" (고양이 174)
            // 가장 오래된 점을 재사용 (최대 = 자국 버퍼 크기)
            int cap = _balance != null ? _balance.ScentMaxMarks : 32;
            Transform dot;
            if (_dots.Count < cap)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ScentDot";
                Destroy(go.GetComponent<Collider>());
                _block ??= new MaterialPropertyBlock();
                _block.SetColor(BaseColorId, new Color(1f, 0.85f, 0.2f));
                go.GetComponent<Renderer>().SetPropertyBlock(_block);
                dot = go.transform;
            }
            else
            {
                dot = _dots[0].t;
                _dots.RemoveAt(0);
            }
            dot.position = pos + Vector3.up * 0.02f;
            dot.gameObject.SetActive(true);
            _dots.Add((dot, Time.time, lifeSeconds));
            DotsShown++;
            if (DotsShown % 10 == 1) Log.Dev($"내 냄새 자국 표시 #{DotsShown}"); // 소유 클라에만 찍힌다 (2인 검증)
        }

        private void Update()
        {
            if (!IsOwner || _dots.Count == 0) return;
            // 강도가 줄어드는 만큼 점도 작아진다 (20s → 0)
            foreach (var (t, born, life) in _dots)
            {
                if (t == null) continue;
                float k = 1f - Mathf.Clamp01((Time.time - born) / life);
                if (k <= 0f) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                t.localScale = new Vector3(0.28f * k + 0.04f, 0.02f, 0.28f * k + 0.04f);
            }
        }

        public override void OnNetworkDespawn()
        {
            foreach (var (t, _, _) in _dots) if (t != null) Destroy(t.gameObject);
            _dots.Clear();
        }
    }
}
