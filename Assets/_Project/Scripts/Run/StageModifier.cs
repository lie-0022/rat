namespace RatGame.Run
{
    /// <summary>오늘의 집 — 스테이지 조건 (docs/10, 고양이 106). 끝에만 추가 (RPC로 byte 전송).</summary>
    public enum StageModifier : byte { None, Blackout /* 정전 */, TrapSale /* 덫 대방출 */, CatTreats /* 고양이 간식 날 */, OwnerOut /* 집주인 외출 */, Busy /* 분주한 집 */, Guest /* 고양이 손님 — 한 마리 더 (고양이 117) */ }
}
