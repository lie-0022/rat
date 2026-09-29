using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    public enum ConditionState { Active, Stunned, Trapped, Downed, Hidden /* 숨을 곳 안 (2026-09-24, design/cat-ideas/14) */, Pinned /* 고양이가 가지고 노는 중 (2026-09-24, design/cat-ideas/04) — append-only */ }

    /// <summary>
    /// 상태이상 (docs/04, 호스트 권한). Stunned=시간 경과 해제, Trapped=동료 Interact 1.5s,
    /// Downed=동료가 쥐구멍까지 운반 — 대리 몸(PlayerDownedBody·World/DownedBody, 2026-09-24 고양이 46)을 끌어 쥐구멍에 넣으면 부활.
    /// Trapped 구출 대상으로서 IInteractable 구현. 래그돌·관전 카메라는 아트/후속 단계.
    /// </summary>
    public class PlayerCondition : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<ConditionState> State = new NetworkVariable<ConditionState>(ConditionState.Active);

        private float _stunEndTime; // 호스트 전용

        public string PromptText => $"구출하기 — {PlayerVisual.ColorNameFor(OwnerClientId)}"; // 누구인지 (고양이 187)
        public float HoldSeconds => _balance.RescueHoldSeconds;

        public bool CanInteract(ulong clientId) =>
            State.Value == ConditionState.Trapped && clientId != OwnerClientId;

        /// <summary>호스트 전용 — Trapped 구출 (docs/04 표).</summary>
        public void ServerInteract(ulong clientId)
        {
            if (!IsServer || State.Value != ConditionState.Trapped) return;
            ServerSetState(ConditionState.Active);
            Log.Dev($"구출: client {clientId} → client {OwnerClientId}");
            EventBus.RaiseRatRescued(clientId, OwnerClientId);
        }

        /// <summary>호스트 전용 상태 전이. Downed/Trapped 진입 시 들고 있던 아이템 드랍 (docs/04·05).</summary>
        public void ServerSetState(ConditionState next)
        {
            if (!IsServer || State.Value == next) return;
            var prev = State.Value;
            State.Value = next;
            Log.Dev($"상태이상: client {OwnerClientId} {prev} → {next}");

            if (next == ConditionState.Stunned)
                _stunEndTime = Time.time + _balance.StunSeconds;

            if (next == ConditionState.Downed || next == ConditionState.Trapped || next == ConditionState.Pinned)
                GetComponent<PlayerCarryController>()?.ServerForceDrop();

            if (prev == ConditionState.Downed && next == ConditionState.Active)
                EventBus.RaisePlayerRevived(OwnerClientId);
            if (next == ConditionState.Downed)
                EventBus.RaisePlayerDowned(OwnerClientId);
        }

        /// <summary>호스트: 정해진 시간만큼 기절 (전기선 등 함정마다 다를 수 있게 — 고양이 49).</summary>
        public void ServerStun(float seconds)
        {
            if (!IsServer || State.Value != ConditionState.Active) return;
            ServerSetState(ConditionState.Stunned);
            _stunEndTime = Time.time + seconds;
        }

        /// <summary>PlayerNoiseEmitter가 착지 시 호출 (호스트) — 6m+ 낙하는 기절 (docs/04).</summary>
        public void ServerOnLanded(float fallHeight)
        {
            if (!IsServer || State.Value != ConditionState.Active) return;
            if (fallHeight > _balance.HighFallStunHeight)
                ServerSetState(ConditionState.Stunned);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (State.Value == ConditionState.Stunned && Time.time >= _stunEndTime)
                ServerSetState(ConditionState.Active);
        }
    }
}
