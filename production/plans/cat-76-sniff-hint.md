# 고양이 76 — 킁킁: 목적지 냄새 줄기 (기획, 2026-09-25)

> 근거: 루프 후보 ⑨ "목적지 방향 힌트". 벽 속은 방 16~18개 + 고리라 목적지 불빛이 안 보이는 곳에서 헤맨다. 사양은 docs/04 "Sniff(킁킁)".

## 규칙 (잠정)
- R → 3초 냄새 줄기(내 화면만) → 다음 문까지. 쿨다운 10초. 소음 없음.
- 다음 문만 — 길 전체를 주지 않아 탐험·갈림길 판단은 남긴다.

## 구현
1. `Run/GridRoute.TryNextHint(from, out target, out roomsLeft)` — GridRoom 그래프 BFS(열린 면 + 그 방향 가장 가까운 방의 반대 면도 열림 = 이웃). DepositZone이 있는 방이 목적지.
2. `Player/PlayerSniff` (소유 클라, 입력 액션 "Sniff" R/패드 십자 아래) → `EventBus.SniffHint`.
3. `UI/SniffTrailView` (Hud) — 알갱이 풀, 다음 문 쪽으로 흘러가며 흐려짐. 머티리얼 에셋 `SniffPuff`.
4. `DevRemoteControl` "sniff" — 2인 확인용.
5. 수치: sniffCooldownSeconds 10, sniffTrailSeconds 3.

## 확인 (계획)
- 1인: 출발방에서 R → 줄기가 출발방 문으로. 목적지방에서 R → 창고로. 통로 안 → 목적지 쪽 끝. 모든 방에서 BFS가 목적지까지 닿음(방 수 로그).
- 2인: 클라에서 sniff → 클라 로그 "킁킁"(클라에도 방 그래프가 맞게 보임).

## 진행 — 구현·확인 (2026-09-25)
- `Run/GridRoute`, `Player/PlayerSniff`(Player 프리팹, PlayerPing과 같은 참조), `UI/SniffTrailView`(Hud 프리팹 자식 `SniffTrail`, 알갱이는 씬 루트 `SniffPuffs` — 캔버스 스케일이 안 곱해지게), 머티리얼 `Art/Materials/SniffPuff`(URP Unlit 반투명 노랑), 입력 액션 Sniff(R·패드 십자 아래), `EventBus.SniffHint`, `DevRemoteControl` "sniff".

| 확인 | 결과 |
|---|---|
| 모든 방 | 시드 1668946556(방 14): 방 14/14·통로 14/14 힌트 성공, 경로를 따라 남은 방 수가 1씩 줄어듦(예: 3→2→1→목적지) |
| 목적지방 | 창고 위치로 |
| 화면 | 출발방에서 R → 노란 알갱이 줄기가 발밑에서 출발방 문으로, 3초 뒤 사라짐 (첫 프레임 청록은 에디터 셰이더 컴파일 중 대체색 — 빌드엔 없음) |
| 2인 | 클라 "킁킁: client 1 다음 목표 (-4,0,0)까지 4.0m, 목적지까지 방 6칸" = 호스트가 같은 위치로 계산한 값, 예외 0 |
