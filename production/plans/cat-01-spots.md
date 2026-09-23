# 고양이 1 — 스팟 그래프 순찰 + 데모 레이아웃 (2026-09-24, 세트 1의 1단계)

> design/cat-design/03 세트 1 첫 태스크. 결정: 감각 단일 게이지 유지, 선반 층·체력 보류(사용자 미결정 상태에서 자율 진행 — 되돌리기 쉬운 것만).

## 목표
웨이포인트 배열 순회를 **스팟 그래프**로 바꾸고, 데모 스테이지에 고양이·스팟·시야 가림 상자를 배치해 실제로 "볼일 보러 다니는" 고양이를 검증한다.

## 설계
- `AI/CatSpot` (종류·가중치·바라보는 방향). 없으면 `CatWaypoint*`를 Look으로 — 기존 씬 회귀 없음.
- `CatBrain` Patrol: 가중치 랜덤(최근 2개 제외) → 도착 → 종류별 머무름(Bed = Sleep 상태 45s 후 깸 / Food 25s 감각 0.5 / Sun 40s 0.5 / Groom 20s 0.6 / Look 2~5s). Return은 가장 가까운 스팟.
- 수치는 BalanceConfigSO `고양이 스팟` 헤더.
- `Tools/RatGame/Cat/Build Demo Layout`: Stage_Warehouse01에 스팟 6·상자 4·고양이 1 + NavMesh.

## 검증
- 에디터 호스트 1인: 스팟 방문 로그가 여러 종류를 거치는지(최근 제외 동작), Bed 도착 → Sleep → 45s 뒤 Patrol, Food 머무는 중 감각 0.5, 자극 시 Suspicious로 끊기고 Return 뒤 이어지는지.
