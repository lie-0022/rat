# 고양이 92 — 순찰꾼 고양이 (기획, 2026-09-25)

> 근거: 팀 대화 "고양이 여러 마리, 기믹을 다르게" — 아기(75)·문지기(80)에 이어 세 번째 역할. 큰길이 위험하면 갈래·배관(79)·보물방(84) 우회 선택이 산다. 사양 docs/07·10.

## 규칙 (잠정)
- 스테이지 4(50%)·5(100%), 문지기·아기가 아닌 어른이 남을 때.
- 큰길(출발→목적지 최단, 배관 제외)의 가운데 방 중심마다 순찰 지점 → 순서대로 오가고 끝에서 되돌아옴, 머묾 짧게.
- 성격 "순찰꾼" 적갈색 — 무작위 제외.

## 구현
1. `CatPersonalitySO._isPatroller`, `RoleOnly`에 포함. `Editor/CatGuardTools`에 순찰꾼 성격 메뉴 추가(목록 끝).
2. `AI/CatBrain.Route.cs`: `ServerMakePatroller(points)`, `TryRouteNext()` — `GoToNextSpot` 맨 앞(밥 시간 다음).
3. `GridZoneBuilder.MaybePatroller`, `GridZoneSO.PatrolChanceByStage`, 안내 플래그 16 + 토스트 고양이 줄 재구성.

## 확인 (계획)
- 스테이지 5: 순찰꾼 배정, 60초 동안 큰길 방 안에 있는 비율·지점 방문 순서(오가기), 다른 역할과 안 겹침. 2인: 클라 성격 "순찰꾼".

## 진행 — 구현·확인 (2026-09-25)
- `AI/CatBrain.Route.cs`(배정 때 큰길 첫 지점으로 순간이동 — 문지기와 같은 이유), `GoToNextSpot` 맨 앞 `TryRouteNext`(딴 데 다녀오면 가까운 지점부터), `GridZoneBuilder.MaybePatroller/MainPath`(배관 뺀 BFS), `Personality_Patroller`(목록 끝 인덱스 6, `Tools/RatGame/Cat/Create Patroller Personality`), 토스트 고양이 줄을 역할 목록으로.

| 확인 (스테이지 5) | 결과 |
|---|---|
| 배정 | 문지기(G)·순찰꾼(P)·아기(K) 셋 다 다른 고양이, 큰길 지점 5개 |
| 60초 | 큰길 방(+3m) 안 100%, 지점 도착 순서 2→3 |
| 방해 뒤 | 아기가 자자 엄마 관계로 같이 잠(문지기도) — 쥐에겐 틈, 그대로 둠 / 집주인 부르기로 40초 부재 → 복귀 뒤 순찰 재개 2→1(되돌아오는 방향) |
| 2인 | 클라 성격 "순찰꾼"·"문지기", 안내 특징 30(문지기·배관·보물방·순찰꾼), 예외 0 |
| 안내 화면 | 역할 셋 다 있을 때 고양이 줄 두 줄 안에 들어감 |
