using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 스테이지 사이에 살아남는 런 기록 (호스트 프로세스, docs/09).
    /// 귀환하면 런 씬을 다시 로드하므로 씬 오브젝트인 RunManager에는 둘 수 없다. 전멸·플레이 시작 시 초기화.
    /// </summary>
    public static class RunSession
    {
        public static int StageNumber { get; private set; } = 1;
        public static int TotalValue { get; private set; }

        public static void AdvanceStage(int haul)
        {
            TotalValue += haul;
            StageNumber++;
        }

        public static void Reset()
        {
            StageNumber = 1;
            TotalValue = 0;
        }

        // 도메인 리로드를 끈 플레이 모드에서도 이전 플레이의 기록이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Reset();
    }
}
