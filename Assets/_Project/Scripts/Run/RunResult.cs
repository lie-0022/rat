using System.Collections.Generic;

namespace RatGame.Run
{
    /// <summary>전멸 결과 (docs/09 — EventBus.RunEnded 페이로드). 결과 화면(2-6)이 소비.</summary>
    public struct RunResult
    {
        public int TotalValue;                       // 저장된 누계 (전멸해도 유지)
        public int CoinsAwarded;                     // 메타 보상 규칙 미정 — 0
        public Dictionary<ulong, int> DepositCounts; // "오늘의 일꾼" 표시용
        public List<string> NewCodexIds;
    }
}
