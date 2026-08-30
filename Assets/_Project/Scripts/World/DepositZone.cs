using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쥐구멍 정산 트리거 — W1 샌드박스용 최소판 (docs/03 목표물의 '정산 로그').
    /// 판정은 호스트 기준만 (docs/03 레이턴시 규칙). 할당량 연동·다운 동료 부활 분기는
    /// RunManager 생기는 태스크 1-7/1-8에서 확장.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DepositZone : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            var item = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<CarryableItem>() : null;
            if (item == null) return;

            int value = item.EffectiveValue; // 금 간 Fragile은 50% (docs/05)
            item.ServerReleaseAll();
            EventBus.RaiseLootDeposited(item.Data, value);
            Log.Dev($"정산: {item.name} → {value} 가치");
            item.NetworkObject.Despawn();
        }
    }
}
