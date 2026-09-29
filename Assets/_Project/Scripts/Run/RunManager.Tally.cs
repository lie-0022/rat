using System.Collections.Generic;
using RatGame.Core;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;

namespace RatGame.Run
{
    /// <summary>
    /// 런 매니저 — 기여 집계(적립·귀환 계산·구조). RunManager.cs가 300줄을 넘어 나눔 (고양이 186).
    /// </summary>
    public partial class RunManager
    {
        public void ServerDeposit(int value, ulong byClientId)
        {
            if (!IsServer || !AcceptsDeposits) return;
            StashedValue.Value += value;
            int i = ContributionIndex(byClientId);
            var c = Contributions[i];
            c.DepositedValue += value;
            c.DepositCount++;
            Contributions[i] = c;
            Log.Dev($"쥐구멍 적립: +{value} → {StashedValue.Value} (client {byClientId})");
        }

        private void CompleteReturn()
        {
            // 귀환자가 손·주머니에 든 물건 (대형을 여럿이 들고 있으면 먼저 센 한 명에게 한 번만)
            var counted = new HashSet<CarryableItem>();
            int carriedValue = 0;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                int i = ContributionIndex(client.ClientId);
                var c = Contributions[i];
                var condition = client.PlayerObject.GetComponent<PlayerCondition>();
                c.Downed = condition != null && condition.State.Value == ConditionState.Downed;
                var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
                if (!c.Downed && carry != null)
                {
                    c.CarriedValue += CountItem(carry.CarriedItem, counted);
                    for (int s = 0; s < carry.SlotCount; s++) c.CarriedValue += CountItem(carry.GetSlotItem(s), counted);
                }
                carriedValue += c.CarriedValue;
                Contributions[i] = c;
            }

            int haul = StashedValue.Value + carriedValue;
            if (_quota != null) _quota.ServerOnCleared(haul); else RunSession.AddHaul(haul); // 누계에 더하고 바로 저장
            ResultCarriedValue.Value = carriedValue;
            RunTotalValue.Value = RunSession.TotalValue;
            ResultEndsAt.Value = NetworkManager.ServerTime.Time + _balance.ResultScreenSeconds;
            ServerAchievementsOnCleared(); // 결과 화면 전에 — 엔딩 여부(Finished)는 위 ServerOnCleared가 정함
            SetPhase(RunPhase.Returned);
            EventBus.RaiseZoneEnded(0, true);
            Log.Dev($"귀환: 쥐구멍 {StashedValue.Value} + 들고 온 {carriedValue} = {haul}, 누계 {RunTotalValue.Value}");
        }

        private static int CountItem(CarryableItem item, HashSet<CarryableItem> counted) =>
            item != null && counted.Add(item) ? item.EffectiveValue : 0;

        // 출발 뒤 들어온 기여자(개발용 직접 플레이 등)도 줄을 만든다
        private int ContributionIndex(ulong clientId)
        {
            for (int i = 0; i < Contributions.Count; i++)
                if (Contributions[i].ClientId == clientId) return i;
            Contributions.Add(new PlayerContribution { ClientId = clientId });
            return Contributions.Count - 1;
        }

        // 끈끈이 구출·쓰러진 몸 부활 (호스트에서만 발생) — 결과 화면 "구조 N" (고양이 186)
        private void OnRatRescued(ulong rescuer, ulong rescued)
        {
            if (!IsServer || rescuer == rescued) return;
            ServerAchievementRescue(rescuer); // 구급 쥐 — 끈끈이 구출·쓰러진 몸 부활 모두 (결과 화면 "구조"와 같은 기준, 고양이 220)
            int i = ContributionIndex(rescuer);
            var c = Contributions[i];
            c.Rescues++;
            Contributions[i] = c;
            Log.Dev($"구조: client {rescuer} → client {rescued} (구조 {c.Rescues})");
        }
    }
}
