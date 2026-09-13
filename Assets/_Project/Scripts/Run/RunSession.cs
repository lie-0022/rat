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

        /// <summary>귀환 정산분을 누계에 더하고 바로 저장.</summary>
        public static void AddHaul(int haul)
        {
            SaveService.Data.HaulTotal += haul;
            SaveService.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => DepartPending = false;
    }
}
