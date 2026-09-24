# 00. 게임 개요 — 쥐도 새도 모르게 (가제)

> 이 문서 팩은 클로드코드가 Unity 프로젝트에서 기본 로직 전체를 구현할 수 있도록 만든 실행 계획이다.
> 문서 번호 순서 = 대략적인 구현 순서. 각 문서는 클래스 단위 설계·시그니처·수치 초기값·수용 기준을 포함한다.

## 한 줄 정의

1~4인 온라인 협동 은신 약탈 게임. 쥐가 되어 인간의 집에 잠입해 전리품을 물리 기반으로 쥐구멍까지 운반하고,
고양이·함정을 피해 파밍한 만큼 살아서 기지로 가져오는 익스트랙션 루프(레포식 — 기지→파밍→귀환 반복. 전멸하면 그 판 적립만 잃고 누계는 유지·저장). 전투 없음. (2026-09-13 개편: 할당량·탈출 투표 삭제, docs/09)

> **새 루프 (2026-09-24 사용자 결정 "단계적으로 적용", 팀 대화 방향)**: 목적지 게시판 "벽 속(생성)"은 **스테이지 1~5 → 엔딩** — 출발방에서 맵 반대편 목적지(식량 창고·상점·안전지대)까지, 스테이지마다 **식량 할당량**을 채워야 넘어간다(남은 식량 = 상점 돈). 위 한 줄 정의의 귀환 루프는 창고·부엌 목적지에 그대로 남아 있다. 사양 docs/09 "새 루프"·docs/10 "생성기 v2".

## 디자인 필러 (구현 중 모든 판단 기준)

1. **물리 운반이 코어** — 물건을 드는 순간부터 웃겨야 한다. 정밀함보다 재미. 물리가 이상해도 웃기면 남긴다.
2. **은신 긴장감** — 소음 시스템이 모든 행동에 리스크를 부여한다. 고양이 상태가 항상 읽혀야 한다.
3. **협동 강제** — 무거운 물건은 혼자 못 든다. 다운된 동료는 운반해서 살린다. 끈끈이는 동료가 떼준다.
4. **귀엽고 댕청하게** — 캐릭터·연출·텍스트 전부. B급 감성 허용.

## 스코프 가드레일 (구현하지 않는 것)

- 전투 시스템 없음 (무기, HP바, 데미지 계산 없음 — 상태이상만 존재)
- 절차적 지형 생성 없음 (방 프리팹 조합 랜덤만)
- 전용 서버 없음 (호스트 리슨 서버만)
- 인벤토리 UI 없음 (손에 든 것 + 등짐 슬롯 수치만)
- PvP 없음

## 핵심 수치 요약 (초기 밸런스 — 전부 ScriptableObject/const로 노출해 튜닝 가능하게)

| 항목 | 값 |
|---|---|
| 플레이어 수 | 1~4 |
| 1런 목표 시간 | 15~25분 |
| 스테이지 구분 | 없음 — 매번 같은 파밍, 누계만 쌓임·저장 (docs/09) |
| 구역당 방 모듈 수 | 3~4 |
| 구역당 고양이 수 | Zone1: 1, Zone3+: 2 |
| 솔로 보정 | 고양이 시야 -20% |

## 시스템 지도 (구현 대상 전체)

```
[Bootstrap/Netcode]  GameBootstrap, NetworkLauncher, SteamLobbyService
        │
[Run Flow]           GameStateMachine, RunManager(스테이지·쥐구멍 적립·귀환·전멸), ZoneGenerator
        │
[Player]             PlayerController, PlayerInteractor, PlayerCarryController,
                     PlayerCondition(다운/속박), PlayerStamina, PlayerNoiseEmitter
        │
[World]              CarryableItem(+Trait), DepositZone(쥐구멍), TrapBase(4종),
                     NoiseSystem, InteractableBase(문·자판기·구출)
        │
[AI]                 CatBrain(FSM), CatSenses(시야·청각), CatMovement
        │
[Meta]               MetaWallet, SaveService, HubManager, VendingMachine,
                     EquipmentSO/SkinSO/CodexEntrySO, AchievementService
        │
[UI]                 HudController, QuotaBar, LobbyUI, PauseMenu, ResultScreen, PingMarker
```

## 문서 목차

| 문서 | 내용 | 구현 주차 |
|---|---|---|
| 01-project-setup | Unity 버전·패키지·폴더·씬 구성 | W1 |
| 02-architecture | 어셈블리·패턴·이벤트·권한 모델 | W1 |
| 03-netcode | NGO+Facepunch 부트스트랩, 동기화 전략 | W1–2 |
| 04-player | 이동·상태·스태미나·상호작용 | W2 |
| 05-carry-system | 잡기·운반·던지기·무게·다인 운반·파손 | W3–4 |
| 06-noise-system | 소음 발생·전파·리스너 | W4 |
| 07-cat-ai | 고양이 FSM·감각·추격 | W5–6 |
| 08-loot-and-economy | 아이템 데이터·가치·지갑 | W5–6 |
| 09-round-flow | 런 상태머신·귀환·다운/구출·전멸 | W6–7 |
| 10-map-generation | 방 모듈 조합·스폰 테이블 | W7–8 |
| 11-hub-and-meta | 허브·상점·장비·스킨·저장 | W8–9 |
| 12-ui | HUD·메뉴·핑 | W9–10 |
| 13-milestones | 주차별 태스크 + 수용 기준 + 테스트 체크리스트 | 전체 |
| 14-claude-code-workflow | 클로드코드 구동 방법·프롬프트 템플릿 | 전체 |

## 용어 통일 (코드·문서 공통)

| 용어 | 코드 명칭 | 의미 |
|---|---|---|
| 파밍 | Run | 기지 출발 → 귀환 또는 전멸까지 1회. 누계는 초기화 없음 |
| 구역 | Zone | 방 3~4개 묶음, 할당량 단위 |
| 방 모듈 | RoomModule | 미리 제작된 방 프리팹 |
| 쥐구멍 | RatHole / DepositZone | 입출구 + 전리품 정산 지점 |
| 전리품 | Loot / CarryableItem | 운반 대상 |
| 귀환 | Return | 다운 안 된 전원이 쥐구멍에 모여 기지로 돌아감 (할당량은 2026-09-13 삭제) |
| 할당량 (새 루프) | StageQuota | 벽 속 스테이지마다 창고에 채울 식량 — 못 채우면 못 넘어감 (2026-09-24 다시 도입, docs/09) |
| 목적지방 (새 루프) | GridRoom Destination | 식량 창고·상점 판매대·안전지대가 있는 방 — 모두 모이면 다음 스테이지 |
| 오늘의 집 | StageModifier | 벽 속 스테이지 조건(정전·덫 대방출·간식 날·집주인 외출·분주한 집·고양이 손님), docs/10 |
| 치즈코인 | CheeseCoin | 메타 재화 |
| 다운 | Downed | 잡힘/함정으로 무력화, 운반 가능 오브젝트화 |
