# 고양이 23 — 두 마리: 엄마·아기 (2026-09-24, design/cat-ideas/07 마지막 관계)

## 목표
아기는 느리고(이동 ×0.75) 시야도 좁고(×0.5 ≈ 4m) **잡지 못한다**(넘어뜨리기만 — 쥐 Stunned). 대신 아기가 놀라면(의심·추격 진입) **"냐앙"** → 엄마가 집 어디서든 **추격 속도로 달려온다**. 아기 옆은 조용히 지나가면 안전, 놀래키면 지옥. 쥐 입장에선 아기를 안 건드리면 사실상 1마리.

## 설계
- `CatPersonalitySO` 필드: `isKitten`, `moveSpeedMultiplier`(1), `bodyScale`(1). 에셋 Personality_Kitten(아기 고양이: 시야 0.5·청각 1·추격 0.6·이동 ×0.75·몸 0.6·수면 ×1.5, 흰 회색).
- 무작위 성격 추첨은 아기를 **빼고** 뽑는다(데모 1마리가 아기로 나오면 안 됨). 아기는 존 생성기·테스트가 `ServerSetPersonality`로 명시.
- `CatMovement.SpeedMultiplier` — 모든 MoveTo 속도에 곱(성격 적용 때 설정).
- 아기 포획: Capture 명중 시 Downed·Toy 대신 쥐를 **Stunned**(기존 2s 기절) → Return.
- `CatRelationKind.MotherKitten`: 고양이 중 아기가 있으면 추첨 없이 이 관계. 아기가 Suspicious·Chase에 들어가면(10s 쿨다운) → 다른(엄마) 고양이 `ServerRespond(아기 위치)` + 18m 안 쥐에게 자막 "(냐앙! — 아기 고양이가 엄마를 부른다)". 공동 수면은 짝꿍처럼.
- `CatState.Respond`(enum 끝): 아기 위치로 추격 속도(5.5) → 도착하면 게이지를 의심 임계로 올려 그 자리 조사(Suspicious). 가는 길에 쥐가 보이면 평소 추격.
- CatVisual: 몸 크기 = 성격 bodyScale. Respond = 추격 색.
- 수치: catKittenCallCooldown 10.

## 검증
- 아기 시야: 5m 쥐 못 봄, 3m 봄.
- 아기 포획 → 쥐 Stunned(Downed 아님).
- 아기가 쥐를 보고 의심 → 엄마 Respond(속도 5.5) → 도착 → Suspicious. 쿨다운 10s.
- 1마리 데모에선 아기 안 뽑힘(무작위 20회).
- 2인: Respond 복제, 아기 몸 크기(클라 연출).

## 검증 결과 (2026-09-24)
- 무작위 성격 200회 추첨 중 아기 0회.
- 1차: 관계가 Buddies로 잠김 — 아기 성격을 스폰 직후 지정하는데 관계는 두 마리를 처음 볼 때 이미 추첨돼서. 수정: 매 스캔 아기가 있으면 MotherKitten으로 덮어씀.
- 2차: 관계 MotherKitten. 아기가 쥐를 **처음 본 거리 4.0m**(그 전까지 4.0m까지 못 봄). 아기 Suspicious → 0.1s 만에 엄마 Respond, 6.5s 동안 최고 5.5로 달려와 아기 3.6m 옆에서 Suspicious. "(냐앙! …)" 자막 표시. 아기 포획 → 쥐 **Stunned**, Downed 없음.
- 아기 몸 크기 0.6(Visual 스케일), 이동 배율 0.75.
- 2인: 빌드 클라 로그 "고양이 성격 연출: Cat(Clone) 아기 고양이 몸 0.6", "고양이 달려감 연출: Cat", 예외 0. (셋업 때 엄마가 스폰 쥐와 Toy 중이라 Respond가 무시 — 규칙대로. 풀린 뒤 발동.)
- 미검증: 아기 공동 수면, 냐앙 쿨다운 10s.
