# 고양이 8 — 냄새 자국 추적 (2026-09-24, 세트 2의 4단계 — 1단계만: Edible·앙심 자국)

## 목표
시야·소리 다음의 감각. 치즈(Edible)를 든 쥐는 바닥에 냄새 자국을 남기고, 고양이는 순찰 중 자국을 맡으면 따라온다. 조용히 잘 숨어도 **뒤늦게 찾아오는** 위험 — 전리품 선택에 고민이 생긴다(치즈는 비싸지만 냄새가 난다). (design/cat-ideas/06)

## 설계
- `Noise/ScentSystem` (정적, 호스트 — NoiseSystem과 같은 모양): `ScentMark{Pos, Strength, Source, Seq, Time}` 링 버퍼 32개, 초당 -3 감쇠, `FindNearest(pos, radius, minStrength, filter)`·`NextInTrail(source, afterSeq)`.
- `Player/PlayerScent` (NetworkBehaviour, Player 프리팹):
  - 호스트: 손에 든 물건 또는 주머니 슬롯에 Edible이 있으면 강도 60, 없어도 어느 고양이에게 찍혔으면(05) 20. 1.5m 걸을 때마다 자국 1개(웅크리면 3m — 웅크림의 가치 +1).
  - 소유 클라에게만 `ScentMarkClientRpc` → 바닥에 노란 점(자기 자국만 보임, 20s 동안 작아지며 사라짐). 남의 자국은 안 보임 → "야 너 냄새 나!".
- `CatState.Track` (`AI/CatBrain.Track.cs`): Patrol·Return 중(우선순위 Suspicious 아래, 호기심 위) 반경 3m 안 강도 ≥8 자국을 맡으면 진입 → 그 자국 → 같은 쥐의 다음 자국 순으로 3.0 속도, 자국마다 킁킁 0.5s. 목격·자극이면 끊김(CheckEscalation). 자국이 끊기면: 끝점 2m 안에 HideSpot이 있으면 **수색**(05의 Search), 아니면 끝점 조사(Suspicious). 따라간 자국(쥐별 Seq)은 다시 안 쫓는다.
- 수색 우선순위: 후보 스팟 2m 안에 자국이 있으면 그 스팟부터(design/cat-ideas/14).
- CatVisual: Track = 주황빛 + 코 킁킁 모션(Search와 같은 모션).
- 수치 (BalanceConfig "냄새"): scentIntervalMeters 1.5 · scentEdible 60 · scentGrudge 20 · scentDecayPerSec 3 · scentMaxMarks 32 · scentCrouchIntervalMul 2 · catScentDetectRadius 3 · catScentMinStrength 8 · catTrackSpeed 3 · catTrackSniffSeconds 0.5.
- 2단계(나중): 젖은 발자국, 물·바람·후추로 지우기, 고양이 침대로 냄새 덮기.

## 검증
- 치즈를 든 쥐가 걸음 → 1.5m마다 자국(웅크리면 3m). 강도 60 → 20s 뒤 사라짐.
- 순찰 고양이 3m 안에 자국 → Track 진입 → 자국 순서대로 따라감 → 끝점에서 Suspicious(또는 숨을 곳 근처면 Search).
- 소유 클라에만 노란 점 (2인: 클라가 치즈 들고 걸으면 클라 로그에 자국 표시, 호스트 화면엔 없음).

## 검증 결과 (2026-09-24)
- 1인: 치즈 조각을 들고 4s 걸음(≈16m) → 자국 11개·내 화면 점 11개(1.5m 간격, 강도 60). 고양이를 자국 시작 2m 옆에 두자 즉시 Track → 11개 모두 따라감(18.4s) → 끝점 근처 숨을 곳 없음 → Return.
- 2인 + 숨기 연계: 호스트가 치즈 들고 장화로 가서 숨음(자국 4 + 숨으며 순간이동한 장화 안 1) → 고양이 Track 5개 → 끝점 3m 안 장화 → **Search(대상 장화)** 8.8s.
- 소유 클라 전용 표시: 호스트 로그 "내 냄새 자국 표시 #1", 빌드 클라 로그 0건(남의 자국 안 보임).
- 조정: 자국 끝 → 숨을 곳 판정 반경 2m → 3m (자국 간격 1.5m + 입구 거리).
- 미검증: 웅크리면 간격 3m, 앙심 자국 20, 수색 후보 중 자국 가까운 스팟 우선.
