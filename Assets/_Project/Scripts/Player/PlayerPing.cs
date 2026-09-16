using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 핑 (docs/04 "카메라 레이캐스트 지점에 3초 마커, 전 클라 표시, 소음 없음, 쿨다운 1s").
    /// 소유 클라가 휠클릭으로 조준 지점을 보내면 서버가 쿨다운만 확인하고 전원에게 중계한다 — 판정이 아니라 표시라 위치 검증은 없음.
    /// 받은 클라는 로컬 EventBus.PingReceived로만 알린다 (마커는 UI가 구독 — 규칙 3).
    /// </summary>
    public class PlayerPing : NetworkBehaviour
    {
        // 네트워크 지연으로 두 요청이 몰려 도착해도 정상 핑을 버리지 않게 서버 쿨다운에 주는 여유
        private const float ServerCooldownSlack = 0.1f;

        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private InputAction _pingAction;
        private float _lastLocalPing = float.NegativeInfinity;
        private double _lastServerPing = double.NegativeInfinity;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            _pingAction = _inputAsset.FindActionMap("Player", true).FindAction("Ping", true);
        }

        private void Update()
        {
            if (!IsOwner || _pingAction == null) return;
            if (_pingAction.WasPressedThisFrame() && !InputFocus.IsUiOpen) TryPing();
        }

        /// <summary>소유 클라: 카메라 정면 조준 지점으로 핑 (쿨다운 중이면 무시). DevRemoteControl "ping"도 이 경로.</summary>
        public void TryPing()
        {
            if (!IsOwner) return;
            if (Time.unscaledTime - _lastLocalPing < _balance.PingCooldownSeconds) return;
            var cam = Camera.main;
            if (cam == null) return;
            _lastLocalPing = Time.unscaledTime;
            PingServerRpc(AimPoint(cam));
        }

        // 가장 가까운 맞은 지점 — 내 몸·손에 든 물건·트리거는 건너뛴다. 안 맞으면 최대 거리 지점
        private Vector3 AimPoint(Camera cam)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, Hits, _balance.PingMaxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            Vector3 point = ray.GetPoint(_balance.PingMaxDistance);
            var carried = GetComponent<PlayerCarryController>()?.CarriedItem;
            for (int i = 0; i < count; i++)
            {
                var col = Hits[i].collider;
                if (col.transform.IsChildOf(transform)) continue;
                if (carried != null && col.transform.IsChildOf(carried.transform)) continue;
                if (Hits[i].distance < best)
                {
                    best = Hits[i].distance;
                    point = Hits[i].point;
                }
            }
            return point;
        }

        [Rpc(SendTo.Server)]
        private void PingServerRpc(Vector3 worldPos)
        {
            double now = NetworkManager.ServerTime.Time;
            if (now - _lastServerPing < _balance.PingCooldownSeconds - ServerCooldownSlack) return;
            _lastServerPing = now;
            PingClientRpc(worldPos);
        }

        [Rpc(SendTo.Everyone)]
        private void PingClientRpc(Vector3 worldPos)
        {
            EventBus.RaisePingReceived(OwnerClientId, worldPos);
            Log.Dev($"핑: client {OwnerClientId} @ {worldPos}");
        }
    }
}
