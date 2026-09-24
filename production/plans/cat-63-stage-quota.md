# 고양이 63 — 새 루프 3단계: 식량 할당량·클리어·굶음 (2026-09-24, docs/09)

## 구현
- `Run/StageQuota`(NV 4개), `RunSession.StageNumber/Pantry/ResetRun`, BalanceConfig `stagesPerRun 5 · stageQuotaBase 150 · stageQuotaPerStage 75`(잠정).
- `RunManager` 연결 3줄(모자라면 카운트다운 안 함 · 클리어 정산 · 전멸 초기화). RunManager 303줄 — 300줄 넘음, 분리 제안.
- `RunBar` 새 루프 문구. `Create Wall Stage` 도구가 StageQuota를 붙임.

## 검증
| 항목 | 결과 |
|---|---|
| 막힘 | 1인·2인 모두 식량 0으로 창고 집합 → StageActive 유지, 안내 "식량이 모자라요 — 150 더" |
| 클리어 | 1인 154 → 150 먹음·남은 4·누계 +150·스테이지 2 / 2인 177 → 남은 27 |
| 다음 스테이지 | 할당량 225, 2/5, 남은 식량 4 이어짐 |
| 굶음 | 스테이지 2에서 전멸 → 스테이지 1·남은 식량 0 |
| 2인 동기화 | 클라 "할당량 연출: 스테이지 1/5 식량 150", 예외 0 |

- 세이브 누계가 바뀌어 사용자 세이브(누계 97523·털 색 #59CCD9)로 되돌림.
