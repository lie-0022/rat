using RatGame.Core;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 씬을 넘어 유지되는 파밍 기록 (호스트 전용, docs/09).
    /// 스테이지 씬은 출발할 때마다 새로 로드되므로 씬 오브젝트인 RunManager에는 둘 수 없다.
    /// 누계는 저장 파일(SaveService)에 있고 초기화되지 않는다 — 전멸해도 유지 (2026-09-14 결정).
    /// </summary>
    public static class RunSession
    {
        public static int TotalValue => SaveService.Data.HaulTotal;

        /// <summary>기지 발판으로 스테이지 씬을 연 경우 — 전원 로드 완료 시 자동 출발.</summary>
        public static bool DepartPending { get; set; }

        /// <summary>기지 목적지 게시판에서 고른 스테이지 (DeparturePad 인덱스). 기지 씬은 귀환 때마다 새로 로드돼서 여기 둔다.</summary>
        public static int StageChoice { get; set; }

        /// <summary>새 루프(docs/09): 이번 런의 스테이지 번호(1부터). 클리어하면 +1, 전멸·엔딩이면 1로.</summary>
        public static int StageNumber { get; set; } = 1;

        /// <summary>새 루프: 할당량을 내고 남은 식량 — 스테이지 사이 상점 돈 (런이 끝나면 0).</summary>
        public static int Pantry { get; set; }

        /// <summary>새 루프 상점에서 산 물건 Id — 다음 맵 출발방에 놓인다(씬이 바뀌면 들고 있던 건 사라져서 "택배"로).</summary>
        public static readonly System.Collections.Generic.List<string> PendingItems = new();

        public static void ResetRun() { StageNumber = 1; Pantry = 0; PendingItems.Clear(); }

        /// <summary>귀환 정산분을 누계에 더하고 바로 저장.</summary>
        public static void AddHaul(int haul)
        {
            SaveService.Data.HaulTotal += haul;
            SaveService.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() { DepartPending = false; StageChoice = 0; ResetRun(); }
    }
}
