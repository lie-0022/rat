using RatGame.Core;
using RatGame.Player;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쥐구멍 = 적립 트리거 + 귀환 지점 (docs/08·09). 판정은 호스트 기준만 (docs/03).
    ///  ① Downed 플레이어 몸 → RunManager.ServerRevive
    ///  ② CarryableItem → 가치 적립 (금 간 Fragile 50%) → RunManager.ServerDeposit + 디스폰
    ///  ③ 귀환 집합 판정 영역(Area) — RunManager가 GatherCheck로 조회
    /// 런이 없으면(RunManager 부재/비활성) 로그 정산만 — 샌드박스 단독 테스트 호환.
    /// 흡입 연출 ClientRpc·도감 언락은 아트/2-5 단계.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DepositZone : NetworkBehaviour
    {
        private BoxCollider _box;

        /// <summary>귀환 집합 판정 영역 (GatherCheck).</summary>
        public BoxCollider Area => _box != null ? _box : _box = GetComponent<BoxCollider>();

        // Stay 판정: 이고 들어와서 안에서 내려놓는 경우도 잡아야 함. 들고 있는 동안(캐리어>0)은 납품 아님 — "두면" 납품.
        private void OnTriggerStay(Collider other)
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

            // ①' 쓰러진 동료의 대리 몸 (고양이 46) — 정산이 아니라 부활. 들고 있는 채로 들어와도 된다
            var body = other.attachedRigidbody.GetComponent<DownedBody>();
            if (body != null)
            {
                if (RunManager.Instance != null) RunManager.Instance.ServerRevive(body.Owner.Value);
                return;
            }

            // ② 전리품 정산
            var item = other.attachedRigidbody.GetComponent<CarryableItem>();
            if (item == null || !item.IsSpawned) return;
            if (item.CarrierIds.Count > 0) return; // 아직 들고 있음 — 내려놓아야 납품
            // 런이 있는데 적립 받는 페이즈가 아니면(출발 전·결과 화면) 물건을 삼키지 않는다 — 가치만 사라지는 함정 방지
            if (RunManager.Instance != null && !RunManager.Instance.AcceptsDeposits) return;

            int value = item.EffectiveValue;
            ulong lastCarrier = item.LastCarrierId;
            EventBus.RaiseLootDeposited(item.Data, value);
            if (RunManager.Instance != null)
                RunManager.Instance.ServerDeposit(value, lastCarrier);
            Log.Dev($"정산: {item.name} → {value} 가치");
            item.NetworkObject.Despawn();
        }

    }
}
