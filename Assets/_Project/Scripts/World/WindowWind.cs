using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 창문 바람 (design/cat-ideas/06·10, 2026-09-24 고양이 37) — 집주인 이벤트 "창문"의 몸체. 열린 동안(호스트):
    ///  냄새 자국을 매초 전부 날리고, 2s마다 돌풍이 풀린 가벼운 물건을 바람 방향(창문 forward)으로 툭 띄워 보낸다 — 굴러가는 물건은 고양이 호기심을 끈다.
    ///  바람 소리가 작은 소리를 묻는다(NoiseSystem 마스크). 창문 판 회전은 Open NV로 전 클라.
    /// </summary>
    public class WindowWind : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private Transform _pane; // 열리면 안쪽으로 젖혀지는 창문 판

        public NetworkVariable<bool> Open = new NetworkVariable<bool>(false);

        private float _until;
        private float _nextClear;
        private float _nextGust;
        private CarryableItem[] _items;
        private Quaternion _paneClosed;

        public Vector3 WindDir { get { Vector3 f = transform.forward; f.y = 0f; return f.normalized; } }

        private void Awake() { if (_pane != null) _paneClosed = _pane.localRotation; }

        public override void OnNetworkSpawn()
        {
            Open.OnValueChanged += (_, open) =>
            {
                if (_pane != null) _pane.localRotation = open ? _paneClosed * Quaternion.Euler(0f, 70f, 0f) : _paneClosed;
                Log.Dev($"창문 연출: {(open ? "열림" : "닫힘")}"); // 2인 검증용
            };
        }

        /// <summary>호스트: 창문 열기.</summary>
        public void ServerStart(float seconds)
        {
            if (!IsServer) return;
            _until = Time.time + seconds;
            _nextClear = 0f;
            _nextGust = Time.time + 0.5f;
            Open.Value = true;
            NoiseSystem.SetMask(this, _balance.WindowMaskLoudness);
            Log.Dev($"창문: 열림 ({seconds}s, 바람 {WindDir:F1})");
        }

        private void Update()
        {
            if (!IsServer || !Open.Value) return;
            if (Time.time >= _until)
            {
                Open.Value = false;
                NoiseSystem.SetMask(this, 0f);
                if (Run.RunManager.Instance != null) Run.RunManager.Instance.ServerHouseEvent(HouseEventKind.Window, HouseEventPhase.End);
                Log.Dev("창문: 닫힘");
                return;
            }
            if (Time.time >= _nextClear)
            {
                _nextClear = Time.time + 1f;
                ScentSystem.Clear(); // 바람이 냄새를 다 날린다
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer || !Open.Value || Time.time < _nextGust) return;
            _nextGust = Time.time + _balance.WindowGustInterval;
            _items = FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            int pushed = 0;
            foreach (var item in _items)
            {
                if (item == null || !item.IsSpawned || item.IsHeavy || item.CarrierIds.Count > 0 || item.Pocketed.Value) continue;
                var rb = item.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic || rb.mass > _balance.WindowMaxItemMass) continue;
                // 물건마다 조금씩 다르게 — 한 줄로 가지런히 밀리면 바람 같지 않다
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f) * WindDir;
                float speed = _balance.WindowGustSpeed * Random.Range(0.7f, 1.3f);
                rb.AddForce(dir * speed + Vector3.up * speed * 0.5f, ForceMode.VelocityChange);
                pushed++;
            }
            Log.Dev($"창문: 돌풍 — 물건 {pushed}개");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NoiseSystem.SetMask(this, 0f);
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance, Transform pane) { _balance = balance; _pane = pane; }
#endif
    }
}
