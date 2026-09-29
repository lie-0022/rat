using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;

namespace RatGame.Run
{
    /// <summary>
    /// 도전과제 통계 (docs/11, 고양이 220). 판정은 호스트(docs/03) — 스테이지 동안 지켜보다가 귀환·클리어 때
    /// 쥐마다 RPC로 통계를 보낸다. 받은 클라는 `EventBus.AchievementStat` → 각자의 AchievementService가 개인 세이브에 쌓는다.
    /// 스테이지 번호(깊은 쥐)는 스테이지 안내 RPC에서, 도감 수는 각자 PlayerCodex에서 바로 올린다.
    /// </summary>
    public partial class RunManager
    {
        private bool _chasedThisStage;
        private int _eggsThisStage;

        private void ServerAchievementsSubscribe()
        {
            CatBrain.ServerStateChanged += OnCatStateForAchievements;
            EventBus.LootDeposited += OnLootForAchievements;
        }

        private void ServerAchievementsUnsubscribe()
        {
            CatBrain.ServerStateChanged -= OnCatStateForAchievements;
            EventBus.LootDeposited -= OnLootForAchievements;
        }

        // 유령 쥐: 이 스테이지에서 고양이가 한 번이라도 추격했나
        private void OnCatStateForAchievements(CatBrain cat, CatState prev, CatState next)
        {
            if (next == CatState.Chase && !_chasedThisStage && Phase.Value == RunPhase.StageActive)
            {
                _chasedThisStage = true;
                Log.Dev("도전과제 판정: 이번 스테이지 추격 있음 (유령 쥐 안 됨)");
            }
        }

        private void OnLootForAchievements(LootItemSO data, int value)
        {
            if (data == null) return;
            if (data.Id == "loot_egg" && value > 0) // 깨진 계란은 0원 — 안 센다
                AllStat("eggsInOneRun", ++_eggsThisStage, true);
            if (data.Id == "loot_chicken") AllStat("chickenDeposits", 1, false);
        }

        /// <summary>호스트: 귀환(창고·부엌) 또는 벽 속 스테이지 클리어 — 결과 화면으로 가기 직전.</summary>
        private void ServerAchievementsOnCleared()
        {
            AllStat("extracts", 1, false);
            if (!_chasedThisStage) AllStat("ghostClears", 1, false);
            // 혼자 살아남기: 혼자 연 방에서 귀환(예전 루프) 또는 엔딩(새 루프 — 스테이지 사이 클리어는 귀환이 아님)
            bool solo = NetworkManager.ConnectedClientsIds.Count == 1;
            bool home = _quota == null || _quota.Finished.Value;
            if (solo && home) AllStat("soloReturns", 1, false);
        }

        /// <summary>호스트: 구조한 쥐에게만 (구급 쥐).</summary>
        private void ServerAchievementRescue(ulong rescuer) => AchievementStatClientRpc("revives", 1, false,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { rescuer } } });

        private void AllStat(string key, int value, bool keepMax) => AchievementStatClientRpc(key, value, keepMax);

        [ClientRpc]
        private void AchievementStatClientRpc(string key, int value, bool keepMax, ClientRpcParams rpcParams = default) =>
            EventBus.RaiseAchievementStat(key, value, keepMax);
    }
}
