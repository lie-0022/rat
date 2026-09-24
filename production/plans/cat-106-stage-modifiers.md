# 고양이 106 — 오늘의 집 (스테이지 조건) (2026-09-25)

> 근거: 5스테이지가 깊이(고양이 수·함정 비율)만 다르다. 스테이지마다 "오늘은 정전" 같은 조건 하나가 있으면 매 판이 덜 똑같다. 사양 docs/10.

## 규칙 (잠정)
- 스테이지 2부터 확률 {0,0.5,0.6,0.7,0.8}로 하나: 정전 · 덫 대방출 · 고양이 간식 날 · 집주인 외출 · 분주한 집.
- 안내 머리줄: "스테이지 3/5 — 식량 300 모아 목적지로 · 오늘: 정전(어두운 방 많음)".

## 구현
- `Run/StageModifier` enum, `GridZoneSO` 값, `GridZoneBuilder`: 시드로 고르고 채우기 전에 적용(어둠·함정), 고양이 스폰 뒤 속도, `Modifier` 속성 → `HouseEventDirector`가 사건 수에 반영.
- `CatBrain.ServerSetStageSpeed` — 성격 속도에 곱하기(성격이 바뀌어도 유지).
- 안내 RPC·EventBus·토스트에 modifier.

## 확인 (계획)
- 조건별로 강제해서: 정전 → 어둠 수↑, 덫 → 함정↑, 간식 → 고양이 agent 속도 ×0.85, 외출 → 사건 0건 예약, 분주 → +2. 안내 문구. 2인: 클라 안내 문구.

## 진행 — 구현·확인 (2026-09-25)
- `Run/StageModifier`, `AI/CatBrain.Stage.cs`(ServerSetStageSpeed — ApplyPersonality가 곱함), `ZonePopulator.DarkChanceOverride`, `GridZoneBuilder.Modifier/OwnerOut/ExtraHouseEvents`, `PickModifier`(맵과 따로 굴림 — seed×31+stage), `HouseEventDirector` 사건 수, 안내 RPC·EventBus·토스트(머리줄 둘째 줄 "오늘: …"), 테스트용 `DevForceModifier`(플레이마다 초기화).
- 분포(시드 1000 × 스테이지): 조건 있음 0/498/603/699/800, 다섯 가지 505~535로 고름.

| 강제 (스테이지 3) | 결과 |
|---|---|
| 없음 | 어둠 9/14, 함정 21/31, 고양이 속도 평균 0.88(아기 0.75 섞임), 사건 2 |
| 정전 | 어둠 **9/10** |
| 덫 대방출 | 함정 18/22 ≈ **0.82**(스테이지 3 비율 0.6+0.2) |
| 고양이 간식 날 | 속도 평균 **0.74**(×0.85) |
| 집주인 외출 | 사건 **0** |
| 분주한 집 | 사건 **5**(2~4 +2) |
| 2인 | 클라 "오늘 Blackout", 예외 0 |
