using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 새 루프의 식량 할당량 (docs/09, 2026-09-24 고양이 63). 이 컴포넌트가 있는 스테이지(벽 속)만 적용 — 창고·부엌은 예전 규칙.
    /// 목적지 창고 적립 ≥ 할당량이어야 전원 집합 카운트다운이 시작된다. 클리어하면 할당량만큼 가족이 먹고(누계에 더함),
    /// 남은 식량은 상점 돈(RunSession.Pantry)으로 넘어간다. 전멸 = 굶음 → 런 처음부터.
    /// </summary>
    public class StageQuota : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<int> StageNumber = new(1);
        public NetworkVariable<int> StagesPerRun = new(1);
        public NetworkVariable<int> Quota = new(0);
        public NetworkVariable<int> Pantry = new(0);
        /// <summary>이번 클리어가 마지막 스테이지였다 — 결과 화면이 엔딩을 띄운다.</summary>
        public NetworkVariable<bool> Finished = new(false);

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                Log.Dev($"할당량 연출: 스테이지 {StageNumber.Value}/{StagesPerRun.Value} 식량 {Quota.Value} 남은 식량 {Pantry.Value}"); // 2인 검증용
                return;
            }
            StageNumber.Value = RunSession.StageNumber;
            StagesPerRun.Value = _balance.StagesPerRun;
            Quota.Value = _balance.StageQuota(RunSession.StageNumber);
            Pantry.Value = RunSession.Pantry;
            Log.Dev($"할당량: 스테이지 {StageNumber.Value}/{StagesPerRun.Value} — 식량 {Quota.Value}, 남은 식량 {Pantry.Value}");
        }

        public bool Met(int stashed) => stashed >= Quota.Value;

        /// <summary>호스트: 클리어 정산. haul = 창고 적립 + 들고 온 것.</summary>
        public void ServerOnCleared(int haul)
        {
            int leftover = Mathf.Max(0, haul - Quota.Value);
            RunSession.AddHaul(Quota.Value);           // 가족이 먹은 만큼 = 누계 (잠정 결정 2)
            RunSession.Pantry += leftover;             // 남은 식량 = 상점 돈 (잠정 결정 1)
            Pantry.Value = RunSession.Pantry;
            bool finished = StageNumber.Value >= StagesPerRun.Value;
            Log.Dev($"스테이지 {StageNumber.Value} 클리어 — 식량 {haul} 중 {Quota.Value} 먹음, 남은 식량 +{leftover} = {RunSession.Pantry}{(finished ? " — 마지막 스테이지 (엔딩)" : "")}");
            Finished.Value = finished;
            if (finished) RunSession.ResetRun();       // 엔딩 → 기지, 다음 런은 처음부터
            else RunSession.StageNumber++;
        }

        /// <summary>호스트: 결과 화면 뒤 갈 씬. 클리어했고 마지막이 아니면 바로 다음 맵(같은 씬을 새로 — 새 시드), 아니면 null = 기지.</summary>
        public string NextSceneAfterResult(bool cleared) => cleared && !Finished.Value ? gameObject.scene.name : null;

        /// <summary>호스트: 전멸 = 굶음 — 런 처음부터.</summary>
        public void ServerOnWiped()
        {
            Log.Dev($"굶음 — 스테이지 {StageNumber.Value}에서 전멸, 런 처음부터");
            RunSession.ResetRun();
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
