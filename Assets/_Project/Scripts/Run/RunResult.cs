using System.Collections.Generic;

namespace RatGame.Run
{
    /// <summary>런 1회 결과 (docs/09 — EventBus.RunEnded 페이로드). 결과 화면(2-6)이 소비.</summary>
    public struct RunResult
    {
        public bool Extracted;                       // false = 전멸
        public int ZonesCleared;
        public int TotalValue;                       // 런 전체 정산 가치
        public int CoinsAwarded;                     // 치즈코인 (docs/08 공식)
        public Dictionary<ulong, int> DepositCounts; // "오늘의 일꾼" 표시용
        public List<string> NewCodexIds;
    }
}
