# 고양이 9 — 가지고 놀기 (2026-09-24, 세트 2의 5단계 — 물고 옮기기는 뺀 1단계)

## 목표
잡혔다고 바로 끝나지 않는다. 고양이는 잡은 쥐를 **툭툭 치고, 놓아주고, 다시 덮친다.** 그 사이 동료에게 구출 창이 열린다 — 동료가 고양이 눈에 띄면(미끼) 고양이가 그쪽으로 튀고 잡힌 쥐는 풀려난다. 협동이 두 배. (design/cat-ideas/04)

## 설계
- `ConditionState.Pinned` (enum 끝): 잡혀 있음 — 이동·잡기 불가(기존 `!= Active` 판정), 찍찍은 가능. 진입 때 든 물건 떨어뜨림(Downed·Trapped와 같음).
- `CatState.Toy` (`AI/CatBrain.Toy.cs`): Capture 명중 시 **다른 쥐 중 움직일 수 있는 쥐(Active·Hidden·Stunned)가 있으면** Downed 대신 Pinned + Toy. 솔로·전원 다운 직전은 지금처럼 즉시 Downed.
  - 관심 100, 전체 30s. 루프: **Bat 2s**(1s마다 앞발로 0.4m 툭 — 소유 클라 TeleportClientRpc) → 관심 -35 → **Release 3s**(쥐 Active, 고양이 1m 물러남).
    - Release 중 쥐가 놓아준 자리에서 2m 넘게 벗어나면 → 고양이 Chase(다시 잡으면 Toy 재개 — 관심·남은 시간 이어서, 10s 안).
    - 안 벗어나면 다시 Pinned → Bat.
  - 관심 ≤ 0 → 하품 2s → 쥐 **살아남음**(Active) → Return.
  - 30s 만료 → 진짜 Downed → Return (그 뒤는 기존 규칙: 쥐구멍으로 운반).
  - **미끼**: 다른 쥐가 고양이 시야에 잡히면 잡힌 쥐 풀어 주고(Active) 그 쥐를 Chase. 즉시 조사 소음(깨짐·찍찍·함정) → 관심 -40.
  - Toy에서 어떤 경로로 나가든 잡힌 쥐가 Pinned로 남지 않게 풀어 준다.
- UI: 팀 칩 "잡힘"(Danger), 토스트 — 남이 잡히면 "○○가 고양이에게 잡혔어요! 눈에 띄어 고양이를 떼어 내요", 내가 풀려나면 "지금 도망쳐!".
- CatVisual: Toy = 진빨강 + 꼬리 느리게 크게 흔듦(놀이).
- 수치 (BalanceConfig "가지고 놀기"): catToySeconds 30 · catToyInterest 100 · catToyLoopDrain 35 · catToyDistractDrain 40 · catToyBatSeconds 2 · catToyNudge 0.4 · catToyReleaseSeconds 3 · catToyEscapeDistance 2 · catToyYawnSeconds 2 · catToyResumeWindow 10.
- 나중: 물고 자기 자리로 옮기기(Carry), 버둥거리기로 굴러가는 방향 조종, 뒤집힌 1인칭 카메라.

## 검증
- 2인(호스트 + 빌드 클라): 고양이가 호스트를 잡음 → Pinned·Toy → Bat·Release 루프 로그 → 관심 소진 → 호스트 Active(생존) / 혹은 30s → Downed.
- 미끼: Toy 중 클라 쥐를 고양이 앞에 → 호스트 풀림 + 클라 Chase.
- Release 중 2m 도주 → Chase.
- 솔로(1인): 잡히면 즉시 Downed (기존 동작 유지).

## 검증 결과 (2026-09-24, 에디터 호스트 + macOS 빌드 클라)
- 솔로(1인): 잡히면 즉시 Downed (기존 유지).
- 미끼: Toy/Bat 1.5s에 클라 쥐를 고양이 앞 3m → 호스트 즉시 Active, 고양이 클라(1) Chase.
- 도주: Release에서 호스트 2.6m 이동 → Chase → 1.0s 뒤 다시 잡음 → **재개**(관심 65·남은 21s 이어짐).
- 질림 생존: Bat→Release 루프 관심 100→65→30→-5 → 하품 → 호스트 Active → Return·Patrol. 하품 뒤 8s 동안 다시 안 잡힘.
- 만료: 관심 감소 5(테스트)로 → 30.9s에 "놀이 끝" → 호스트 Downed.
- 버그 2개 수정: ① 질려서 놓아준 뒤 바로 옆이라 즉시 재포획 → 하품+6s 못 본 척(`CatSenses.IgnorePlayer`, catToyBoredIgnoreSeconds 6). ② 만료가 Release 창에 걸리면 고양이와 2.5m 거리 판정으로 빠져나가 새 30s 놀이가 무한 반복 → 도망 못 친 쥐는 만료 시 무조건 Downed.
- 미검증: 토스트 문구 화면 확인(빌드 로그에 안 찍힘), 깨짐 소리 관심 -40.
