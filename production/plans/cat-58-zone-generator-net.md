# 고양이 58 — 존 생성기 2단계: 네트워크 생성 스테이지 (2026-09-24, docs/10 태스크 2-1)

## 목표
고양이 57의 방 접합을 실제로 플레이할 수 있는 스테이지로. 호스트가 만들고 클라는 스폰을 받는다.

## 구현
- `Run/ZoneBuilder` — 방 스폰 → 런타임 NavMesh(RoomStatic·NoiseBlocker) → 쥐구멍 → 전리품 70%(테이블 가중치, 선반 위 Large 금지, 보너스방 ×3) → 함정 50%(쥐구멍방 제외) → 고양이 → 쥐 배치.
- `Data/SpawnTableSO`, `Data/TrapTableSO`, `ZoneDefinitionSO` 필드 4개.
- `RunManager.UpdateReturn` — 쥐구멍을 늦게 찾음(생성 순서 때문).
- `Editor/ZoneTools` — Create Generated Stage(쥐구멍·함정 프리팹, 테이블 에셋, Stage_Generated 씬, 빌드 세팅), 그레이박스 머티리얼을 에셋으로 저장.

## 찾은 것
- 방·함정 프리팹이 **마젠타** — 에디터에서 만든 머티리얼이 에셋이 아니라서 프리팹에 저장이 안 됨(씬은 됨). `Art/Materials/Greybox/Greybox_RRGGBB.mat`으로 저장해 해결. 재생성 뒤 렌더러 108개 중 셰이더 오류 0.

## 검증
| 항목 | 결과 |
|---|---|
| 에디터 직접 플레이 | 방 5·전리품 23·함정 4·고양이 1, 쥐는 쥐구멍방에 섬, 렌더러 108개 오류 0 |
| 2인(에디터 호스트 + 빌드 클라) | 클라 접속 → 스폰된 NetworkObject 31/31 클라에게 보임, 클라 쥐 y 0.6(쥐구멍 2.5m) |
| 라운드 흐름 | 출발 → 사탕 적립 12 → 둘 다 쥐구멍 → Returning(9s) → Returned(12s) → Hub(20s), Hub 접속 2명 |
| 예외 | 호스트 0, 클라 0 (클라 "no valid NavMesh" 1줄은 고양이 16에서 확인한 무해한 경고 — 클라는 NavMesh를 굽지 않고 에이전트도 끈다) |

- 테스트가 save.json 누계를 97523→97535로 바꿔서 원본으로 되돌렸다.

## 남은 것 (docs/10)
- 방 안 HideSpot·LightZone 배치, 기지 출발 발판을 생성 스테이지로(지금은 Warehouse), 존 전환(3개 연속), 반지 스폰 규칙.
