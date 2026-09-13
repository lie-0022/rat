using System.Collections.Generic;

namespace RatGame.Run
{
    /// <summary>런 1회 결과 (docs/09 — EventBus.RunEnded 페이로드, 런은 전멸로만 끝남). 결과 화면(2-6)이 소비.</summary>
    public struct RunResult
    {
        public int ZonesCleared;                     // 귀환에 성공한 스테이지 수
        public int TotalValue;                       // 기지로 가져온 런 누계
        public int CoinsAwarded;                     // 메타 보상 규칙 미정 — 0
        public Dictionary<ulong, int> DepositCounts; // "오늘의 일꾼" 표시용
        public List<string> NewCodexIds;
    }
}
