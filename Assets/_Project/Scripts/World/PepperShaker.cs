using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 후추통 (design/cat-ideas/06 "후추가 마크를 지운다", 2026-09-24 고양이 33). 들고 던지는 작은 통.
    /// 던져서 착지하거나 세게 떨어지면 한 번 쏟아진다 → 바닥 반경 안 냄새 자국을 계속 지우고,
    /// 들어온 고양이는 재채기로 멈춘다(CatBrain.CheckPepper). 판정은 호스트, 패치 표시는 PatchActive NV로 전 클라.
    /// 물웅덩이와 달리 쥐를 적시지 않는다 — 깨끗하지만 한 번뿐.
    /// </summary>
    public class PepperShaker : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<bool> Spilled = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> PatchActive = new NetworkVariable<bool>(false);
        public NetworkVariable<Vector3> PatchCenter = new NetworkVariable<Vector3>(Vector3.zero);

        /// <summary>호스트: 지금 활성인 후추 패치들 (고양이 재채기 판정용).</summary>
        public static readonly List<PepperShaker> ActivePatches = new();

        private CarryableItem _carryable;
        private float _patchUntil;
        private Transform _patchVisual;

        public float Radius => _balance != null ? _balance.PepperRadius : 2f;

        private void Awake() => _carryable = GetComponent<CarryableItem>();

        public override void OnNetworkSpawn()
        {
            PatchActive.OnValueChanged += (_, on) => ShowPatch(on);
            ShowPatch(PatchActive.Value);
        }

        public override void OnNetworkDespawn() => ActivePatches.Remove(this);

        public bool Contains(Vector3 pos)
        {
            Vector3 d = pos - PatchCenter.Value; d.y = 0f;
            return d.sqrMagnitude <= Radius * Radius;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || Spilled.Value) return;
            bool thrown = _carryable != null && _carryable.IsRecentlyThrown;
            if (!thrown && collision.relativeVelocity.magnitude < _balance.PepperSpillImpactSpeed) return;
            ServerSpill(_carryable != null ? _carryable.AttributedClient : null);
        }

        /// <summary>호스트: 쏟아짐 (충돌 판정 또는 테스트).</summary>
        public void ServerSpill(ulong? byClient)
        {
            if (!IsServer || Spilled.Value) return;
            Spilled.Value = true;
            PatchCenter.Value = new Vector3(transform.position.x, 0f, transform.position.z);
            PatchActive.Value = true;
            _patchUntil = Time.time + _balance.PepperSeconds;
            if (!ActivePatches.Contains(this)) ActivePatches.Add(this);
            Log.Dev($"후추: 쏟아짐 @ {PatchCenter.Value:F1} (client {(byClient.HasValue ? byClient.Value.ToString() : "-")}, {_balance.PepperSeconds}s)");
        }

        private void Update()
        {
            if (!IsServer || !PatchActive.Value) return;
            if (Time.time >= _patchUntil)
            {
                PatchActive.Value = false;
                ActivePatches.Remove(this);
                Log.Dev("후추: 날아감");
                return;
            }
            ScentSystem.EraseInRadius(PatchCenter.Value, Radius); // 코를 마비시킨다 — 안의 자국은 없는 것
        }

        private void ShowPatch(bool on)
        {
            if (_patchVisual == null && on)
            {
                // 통은 들고 다니므로 패치는 통의 자식이 아니라 월드에 둔다
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "PepperPatch";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.22f, 0.17f, 0.12f));
                go.GetComponent<Renderer>().SetPropertyBlock(block);
                _patchVisual = go.transform;
            }
            if (_patchVisual != null)
            {
                Vector3 c = PatchCenter.Value;
                _patchVisual.position = new Vector3(c.x, 0.025f, c.z);
                _patchVisual.gameObject.SetActive(on);
            }
            Log.Dev($"후추 연출: {(on ? "쏟아짐" : "날아감")}"); // 2인 검증용
        }

        public override void OnDestroy()
        {
            if (_patchVisual != null) Destroy(_patchVisual.gameObject);
            base.OnDestroy();
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
