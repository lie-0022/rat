# 고양이 기획 v2 — 움직임과 맵핑 (2026-09-18)

`design/cat-ideas/`(14안)가 "무엇을 넣을까"였다면, 여기는 **"어떻게 움직이고, 맵과 어떻게 맞물리나"**를 여러 모델로 나란히 놓고 고르는 문서다. 아직 결정하지 않는다 — 각 축마다 후보 A/B/C…를 장단점과 함께 적고, 마지막에 추천 조합을 둔다. 확정되면 docs/07·10에 옮겨 적는다(docs가 정답).

| 문서 | 내용 |
|---|---|
| [01-movement-logic.md](01-movement-logic.md) | 순찰 모델 · 감각 모델 · 추격/추적 모델 · 이동 실행 방식 · 텔레그래프 계약 · 속도와 체력 · 난이도 손잡이 |
| [02-mapping.md](02-mapping.md) | 스팟 카탈로그 · 배치 모델 · 통로 문법(쥐/고양이/공용, 높이) · 안전지대 언어 · 스폰 규칙 · 데모 스테이지 배치안 · 검증 |
| [03-recommendation.md](03-recommendation.md) | 축별 추천 조합 3세트(가벼움/기본/풍성)와 구현 순서 초안 |
| [04-implemented-summary.md](04-implemented-summary.md) | **실제로 구현된 것 한눈에** (2026-09-24 자율 루프 고양이 1~26) — 상태 19개 표·시스템·검증 방식·남은 것 |

## 현재 상태 (기준점)
- 코드: `CatBrain` 7상태 FSM, `CatSenses` 시야(원뿔·Linecast·웅크림 절반·어둠 절반)+청각(NoiseSystem), `CatMovement` = NavMeshAgent 래퍼(MoveTo/Stop/RandomPointAround). 순찰 = 씬의 `CatWaypoint*` 이름 검색 → 배열 순회 + 2~5s 대기.
- 맵: 방 모듈 프리팹 규약(docs/10)에 `CatWaypoints[] 2~4`, `CatSpawn`, `DarkZones[]`. 스테이지 `Stage_Warehouse01`은 아직 고양이·웨이포인트 없음(테스트 때만 런타임 스폰).
- 밸런스 축: sprint 6(원래 7) > chase 5.5 — 직선으로는 도망칠 수 있다. 이건 유지.

## 판단 기준
docs/00 필러: ① 물리 운반 코어 ② 은신 긴장감 — 고양이 상태가 항상 읽혀야 함 ③ 협동 강제 ④ 귀엽고 댕청. 가드레일: 전투·절차지형·PvP 없음. 그리고 실무 기준 둘: **호스트 한 대 CPU**(고양이 최대 2마리, 4클라), **그레이박스로 먼저 검증 가능한가**.
