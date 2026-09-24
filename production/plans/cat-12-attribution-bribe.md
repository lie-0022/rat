# 고양이 12 — 소음 귀속 + 깨뜨린 쥐 앙심 + 유인 속음 + 뇌물 (2026-09-24, 05 후속)

## 목표
앙심(고양이 7)의 남은 조각. 누가 낸 소리인지 분명하게 해서 "접시 깬 쥐"를 고양이가 기억하고, 털실에 속은 고양이는 던진 쥐를 찍고(그 쥐의 유인엔 다시 안 속는다), 찍힌 쥐는 **치즈를 바쳐** 풀 수 있다 — 가치 버려 목숨 사기. (design/cat-ideas/05)

## 설계
- **소음 귀속**: `NoiseEvent.HasSource` 추가. `NoiseSystem.Emit(..., ulong? source)` — 플레이어 소리(발소리·찍찍·착지·헐떡임·숨기)는 그대로 귀속, 환경음은 없음.
  - 물건 충돌·깨짐: `CarryableItem.AttributedClient` — 들고 있으면 첫 캐리어, 놓은(던진) 지 3s 안이면 마지막 캐리어, 그 밖은 없음. 호스트 id 0과 "환경 0"이 섞이던 문제 해결.
- **깨뜨린 쥐**: 귀속된 깨짐(Break)을 들으면 그 쥐 앙심 +30.
- **유인 속음**: 털실이 던진 쥐에 귀속되면 `ServerDistract(..., fooledBy)` → 놀이가 끝나면 "속았다" 앙심 +20. 그 쥐가 이미 찍혀 있으면(≥50) **안 속는다**.
- **뇌물**: 순찰·복귀·의심·수색 중(추격 아님) 고양이 정면 60° 1.5m 안에 쥐가 **10s 안에 내려놓은** 치즈류(Edible)가 있으면 → 먹는다(Distracted 4s, 그 자리) → 물건 사라짐 → 놓은 쥐 앙심 -50.
- 수치: noiseAttributionSeconds 3 · catGrudgeBreak 30 · catGrudgeFooled 20 · catGrudgeBribe 50 · catBribeRadius 1.5 · catBribeRecentSeconds 10 · catBribeEatSeconds 4.
- 파일: CarryableItem은 386줄(300 초과) — 필드 2개만 추가하고 분리는 제안으로 남김.

## 검증
- 쥐가 던진 접시(Fragile)가 깨짐 → 앙심 +30 (환경 깨짐은 0).
- 털실 던짐 → Distracted → 끝나면 +20 로그. 찍힌 쥐가 던지면 무시.
- 앙심 80인 쥐가 치즈를 고양이 앞에 내려놓음 → 먹음 4s → 치즈 사라짐 → 앙심 30.

## 검증 결과 (2026-09-24, 에디터 호스트 1인 — 호스트 id 0이 가장 헷갈리는 경우)
- 쥐가 집었다 떨어뜨린 접시 A: AttributedClient 0 → 깨짐 → 앙심 0 → 30.
- 아무도 안 만진 접시 B: AttributedClient 없음 → 깨짐 → 앙심 변화 없음(30 유지). ← 전에는 둘 다 Source 0이라 구분 불가였다.
- 털실 속음(fooledBy 0): Distracted 1.5s 끝 → 앙심 +20(49).
- 앙심 79(찍힘)인 쥐의 유인 → "안 속음", 상태 Patrol 유지.
- 뇌물: 쥐가 치즈를 집었다 내려놓음 → 고양이 앞 1m → "뇌물 발견" → 4.0s 먹음 → 치즈 사라짐 → 앙심 79 → 28.
- 2인 생략: NV·RPC 변경 없음(호스트 로직만). 클라 쥐의 귀속은 같은 코드 경로(LastCarrierId = 클라 id).
- 제안: CarryableItem.cs 386줄 → 300줄 초과. 트레잇 처리(Slippery·Alarming·Fragile)를 `CarryableTraits` 컴포넌트로 분리하는 리팩토링 후보.
