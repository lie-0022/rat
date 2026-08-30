namespace RatGame.Run
{
    /// <summary>런 1회의 결과 요약. 세부 필드는 docs/09-round-flow.md 구현 시 확장 (태스크 1-8).</summary>
    public readonly struct RunResult
    {
        public readonly bool Escaped;        // true=탈출 성공, false=전멸/중단
        public readonly int ZonesCleared;    // 통과한 구역 수
        public readonly int TotalDeposited;  // 정산된 총 가치

        public RunResult(bool escaped, int zonesCleared, int totalDeposited)
        {
            Escaped = escaped;
            ZonesCleared = zonesCleared;
            TotalDeposited = totalDeposited;
        }
    }
}
