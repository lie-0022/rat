using RatGame.Core;
using RatGame.Player;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쥐구멍 정산 트리거 (docs/08). 판정은 호스트 기준만 (docs/03).
    ///  ① Downed 플레이어 몸 → RunManager.ServerRevive
    ///  ② CarryableItem → 가치 정산 (금 간 Fragile 50%) → RunManager.ServerDeposit + 디스폰
    /// 런이 없으면(RunManager 부재/비활성) 로그 정산만 — 샌드박스 단독 테스트 호환.
    /// 흡입 연출 ClientRpc·도감 언락은 아트/2-5 단계.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DepositZone : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || other.attachedRigidbody == null) return;

            // ① 다운된 동료 → 부활 (docs/09)
            var condition = other.attachedRigidbody.GetComponent<PlayerCondition>();
            if (condition != null)
            {
                if (condition.State.Value == ConditionState.Downed && RunManager.Instance != null)
                    RunManager.Instance.ServerRevive(condition.OwnerClientId);
                return;
            }

            // ② 전리품 정산
            var item = other.attachedRigidbody.GetComponent<CarryableItem>();
            if (item == null) return;

            int value = item.EffectiveValue;
            ulong lastCarrier = 0;
            if (item.CarrierIds.Count > 0) lastCarrier = item.CarrierIds[0];
            item.ServerReleaseAll();
            EventBus.RaiseLootDeposited(item.Data, value);
            if (RunManager.Instance != null)
                RunManager.Instance.ServerDeposit(value, lastCarrier);
            Log.Dev($"정산: {item.name} → {value} 가치");
            item.NetworkObject.Despawn();
        }
    }
}
