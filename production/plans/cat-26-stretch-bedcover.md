# 고양이 26 — 작은 조각 둘: 게으름뱅이 기지개 + 고양이 침대로 냄새 덮기 (2026-09-24)

## 1. 게으름뱅이 기지개 (design/cat-ideas/01)
- 게으름뱅이가 잠에서 깨어 의심으로 넘어갈 때(HalfAwake → Suspicious) 바로 움직이지 않고 **1.5s 기지개**(`CatBlunderKind.Stretch`, 몸 늘어남 연출) → 그 뒤 Suspicious. 잠에서 깨는 순간이 읽히는 텔레그래프 — 쥐가 도망칠 틈.
- 성격 필드 `wakeStretchSeconds`(게으름뱅이 1.5, 나머지 0).

## 2. 고양이 침대로 냄새 덮기 (design/cat-ideas/06)
- 쥐가 Bed 스팟 1.5m 안에 들어가면 **고양이 냄새로 덮여** 이후 30s 동안 자국을 안 남긴다(치즈를 들어도). 대신 침대는 고양이가 돌아오는 곳 — 리스크.
- `PlayerScent.ServerCoverScent(30s)` — 호스트가 Bed 스팟 근처 쥐를 매 프레임 확인(CatBrain이 스팟을 안다 → 고양이 쪽에서 판정: 가장 단순).
- 수치: bedCoverRadius 1.5 · bedCoverSeconds 30.

## 검증
- 게으름뱅이 얕은 잠 + 큰 소리 → HalfAwake → 목격/자극 → Stretch 1.5s → Suspicious. 사냥꾼은 바로 Suspicious.
- 치즈 든 쥐가 침대 밟고 걸음 → 30s 자국 0, 이후 다시 자국.

## 검증 결과 (2026-09-24)
- 기지개: 얕은 잠 + 깨짐 60 → HalfAwake → 깨짐 60 → 게으름뱅이 **Stretch 1.5s → Suspicious**, 사냥꾼은 0.0s에 바로 Suspicious. (첫 시도는 Impact 90이 얕은 잠 게으름뱅이 게이지 임계 미달 — 깨짐류로 바꿈)
- 냄새 덮기: 치즈 들고 강도 60 → 고양이 침대 위 → 덮임·강도 0 → 3s 걸어도 자국 0. 만료 뒤 복귀는 같은 코드 경로(덮임 false → 치즈 60)라 추가 측정 생략(재시도 때 치즈를 떨어뜨린 상태였음).
- 2인 생략: 호스트 로직 + BlunderKind 값 하나(검증된 복제 패턴).
