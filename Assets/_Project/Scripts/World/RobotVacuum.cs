using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 로봇청소기 (design/cat-ideas/10, 2026-09-24) — 집주인 이벤트 "청소기"의 몸체. 호스트가 움직이고 NetworkTransform으로 복제.
    /// 켜지면 충전대 → 방 둘레 사각 루프를 돌고, 시간이 끝나면 충전대로 돌아간다. 키네마틱이라 바닥 물건을 밀고 다닌다(필러 1).
    /// 도는 동안 NoiseSystem.MaskLoudness를 올려 작은 소리(발소리·찍찍)를 묻는다 — 고양이는 못 듣는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RobotVacuum : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private float _loopHalfSize = 12f;   // 방 둘레 루프 반폭 (m)

        public NetworkVariable<bool> Running = new NetworkVariable<bool>(false);

        private Rigidbody _rb;
        private Vector3 _dock;
        private Vector3[] _route;
        private int _next;
        private float _until;
        private bool _returning;

        public Vector3 Position => transform.position;

        public override void OnNetworkSpawn()
        {
            Running.OnValueChanged += (_, on) => Log.Dev($"청소기 연출: {(on ? "켜짐" : "꺼짐")} @ {transform.position:F1}"); // 2인 검증용
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _dock = transform.position;
        }

        /// <summary>호스트: 청소 시작.</summary>
        public void ServerStart(float seconds)
        {
            if (!IsServer) return;
            float h = _loopHalfSize, y = _dock.y;
            _route = new[] { new Vector3(-h, y, -h), new Vector3(h, y, -h), new Vector3(h, y, h), new Vector3(-h, y, h) };
            // 충전대에서 가장 가까운 꼭짓점부터
            float best = float.MaxValue;
            for (int i = 0; i < _route.Length; i++) { float d = Vector3.Distance(_route[i], _dock); if (d < best) { best = d; _next = i; } }
            _until = Time.time + seconds;
            _returning = false;
            Running.Value = true;
            NoiseSystem.SetMask(this, _balance.VacuumMaskLoudness);
            Log.Dev($"청소기: 켜짐 ({seconds}s, 소리 {NoiseSystem.MaskLoudness} 미만 묻힘)");
        }

        private void FixedUpdate()
        {
            if (!IsServer || !Running.Value) return;
            Vector3 target;
            if (!_returning && Time.time >= _until)
            {
                _returning = true;
                NoiseSystem.SetMask(this, 0f);
                Log.Dev("청소기: 청소 끝 — 충전대로");
                if (Run.RunManager.Instance != null) Run.RunManager.Instance.ServerHouseEvent(HouseEventKind.Vacuum, HouseEventPhase.End);
            }
            target = _returning ? _dock : _route[_next];
            Vector3 to = target - _rb.position; to.y = 0f;
            float step = _balance.VacuumSpeed * Time.fixedDeltaTime;
            if (to.magnitude <= step)
            {
                _rb.MovePosition(new Vector3(target.x, _rb.position.y, target.z));
                if (_returning) { Running.Value = false; Log.Dev("청소기: 충전대 도착"); return; }
                _next = (_next + 1) % _route.Length;
                return;
            }
            Vector3 dir = to.normalized;
            _rb.MovePosition(_rb.position + dir * step);
            _rb.MoveRotation(Quaternion.LookRotation(dir));
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NoiseSystem.SetMask(this, 0f);
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
