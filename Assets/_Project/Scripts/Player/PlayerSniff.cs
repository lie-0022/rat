using RatGame.Core;
using RatGame.Data;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 킁킁 (docs/04 Sniff, 고양이 76). R → 목적지 쪽 다음 문까지 냄새 줄기를 내 화면에만.
    /// 소음·판정이 없는 표시라 소유 클라에서 끝난다(동기화 없음). 줄기 그리기는 UI가 EventBus로 받는다(규칙 3).
    /// </summary>
    public class PlayerSniff : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction _sniffAction;
        private float _lastSniff = float.NegativeInfinity;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner || _inputAsset == null) return;
            _sniffAction = _inputAsset.FindActionMap("Player", true).FindAction("Sniff", true);
        }

        private void Update()
        {
            if (!IsOwner || _sniffAction == null) return;
            if (_sniffAction.WasPressedThisFrame() && !InputFocus.IsUiOpen) TrySniff();
        }

        /// <summary>소유 클라: 쿨다운이 지났으면 냄새 줄기. DevRemoteControl "sniff"도 이 경로.</summary>
        public void TrySniff()
        {
            if (!IsOwner || Time.unscaledTime - _lastSniff < _balance.SniffCooldownSeconds) return;
            Vector3 from = transform.position;
            if (!GridRoute.TryNextHint(from, out var target, out int roomsLeft))
            {
                Log.Dev("킁킁: 냄새가 안 남 (목적지 없음)");
                return;
            }
            _lastSniff = Time.unscaledTime;
            EventBus.RaiseSniffHint(from, target, _balance.SniffTrailSeconds);
            Log.Dev($"킁킁: client {OwnerClientId} 다음 목표 {target:F1}까지 {Vector3.Distance(from, target):F1}m, 목적지까지 방 {roomsLeft}칸");
        }
    }
}
