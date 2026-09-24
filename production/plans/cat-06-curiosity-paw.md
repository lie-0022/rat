# 고양이 6 — 호기심 앞발 (2026-09-24, 세트 2의 2단계)

## 목표
고양이가 **움직이는 물건**에 끌려 가서 앞발로 툭툭 친다. 필러 1(물리 운반)과 고양이가 처음으로 같은 시스템을 공유: 쥐가 굴린 캔이 고양이를 끌어가고, 고양이가 친 물건이 굴러간다. 털실뭉치만이 아니라 **모든 굴러가는 것**이 유인 도구가 된다. (design/cat-ideas/03)

## 설계
- `CatSenses` 세 번째 감각 — 움직임 감지 (시야 틱 0.2s에 같이): 풀린 `CarryableItem`(안 들림·주머니 아님·대형 아님) 중 속도 ≥ 1.5 m/s, 시야 거리·각도 안, 가려지지 않은 것 → `CuriosityTarget`(가장 가까운 것). 목록은 2s마다 다시 모음.
- 우선순위: Chase > Suspicious(플레이어 자극) > **Curious** > Patrol/Return. Patrol·Return 틱에서만 진입 — 의심·추격 중엔 안 속는다, 잠든 고양이도 안 속는다.
- `CatState.Curious` (`AI/CatBrain.Curious.cs` partial):
  - 물건까지 4.0 속도로 이동(물건이 굴러가면 따라감). 수평 0.9m 안이면 멈추고 마주 봄.
  - 앞발 3~5회, 0.6s 간격: 수평 랜덤(정면 ±60°) + 위 0.3, 속도 변화 1.2 m/s (호스트 물리, 규칙 1). `PawTick` NV로 클라 연출.
  - 다 치면 하품 5s → Return. 그 물건은 쿨다운 10s 동안 무시.
  - 지루함: 같은 물건에 누적 30s 넘으면 60s 무시 목록 (무한 유인 방지). 8s 안에 못 닿으면(선반 위·NavMesh 밖) 포기.
  - 물건을 쥐가 집으면 즉시 끝(Return — 쥐 목격은 시야가 처리).
  - 자기가 친 물건의 충돌 소음(Impact)은 무시(물건 1.5m 안). 깨짐(Break)은 그대로 — 자기 발에 놀라는 개그(design/cat-ideas/11).
- 연출: CatVisual Curious = 연두 + 꼬리 곧추 파르르, 앞발 칠 때 몸이 앞으로 튐.
- 수치 (BalanceConfig "호기심"): catCuriosityMinSpeed 1.5 · catCuriousSpeed 4 · catPawCountRange (3,5) · catPawIntervalSeconds 0.6 · catPawImpulse 1.2 · catPawReach 0.9 · catCuriousYawnSeconds 5 · catCuriousCooldownSeconds 10 · catBoredomSeconds 30 · catBoredIgnoreSeconds 60 · catCuriousReachTimeout 8.

## 검증
- 순찰 중 고양이 앞 5m에 물건을 3m/s로 굴림 → Curious 진입 → 도착 → 앞발 n회 로그·물건 속도 변화 → 하품 → Return.
- 쿨다운 10s 안에 같은 물건 다시 굴림 → 무시. 다른 물건 → 반응.
- 의심 상태(게이지 ≥30 자극 직후)에 굴림 → 무시.
- 쥐가 치는 도중 집음 → Return.
- 2인: 클라 화면에서 물건이 앞발에 밀려 움직이는지(호스트 물리 → NetworkTransform/Rigidbody 동기화).

## 검증 결과 (2026-09-24)
- 게으름뱅이(시야 6.4m) — 첫 시도는 6m 밖에서 굴려 못 봄(시야 한계, 정상). 3m 앞에서 굴리자 0.2s 만에 Curious.
- 앞발 5회(0.6~1.2s 간격 — 사이사이 굴러간 치즈를 따라감, 칠 때 거리 0.74~0.91m), 친 뒤 치즈 최고 속도 1.25 m/s(< 1.5라 스스로 재유인 안 됨) → 하품 → Return 9.4s.
- 쿨다운 10s 안에 다시 굴림 → 무시(Return 유지). 소음 90으로 게이지 38 만든 뒤 굴림 → Suspicious 유지(안 속음).
- 2인: 빌드 클라 로그에 "앞발 연출 #1~#5" — PawTick 복제 확인. 콘솔 에러 없음.
- 미검증: 질림 60s(누적 30s 필요), 8s 못 닿음 포기, 쥐가 치는 도중 집기.
