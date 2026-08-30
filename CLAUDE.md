# CLAUDE.md — 쥐도 새도 모르게 (가제)

1~4인 협동 은신 약탈 게임. Unity 6000.3 LTS + URP + Netcode for GameObjects 2.x + Facepunch Steamworks.
**모든 사양은 docs/에 있다. 구현 전 해당 문서를 반드시 읽어라. 문서가 정답이고, 어긋나면 문서를 먼저 고친다.**

## 문서 맵

- docs/00-overview.md — 게임 정의·필러·스코프 가드레일·용어
- docs/01~03 — 셋업·아키텍처·넷코드 (권한 모델은 03이 최종)
- docs/04~10 — 시스템별 사양 (클래스·수치·수용 기준 포함)
- docs/11~12 — 메타·UI
- docs/13 — 태스크 목록 (작업 단위), docs/14 — 작업 방식

## 절대 규칙

1. **호스트 권한**: 물리(전리품)·AI·판정은 호스트에서만. 플레이어 이동만 예외(소유 클라 권한). docs/03의 표를 벗어나는 동기화 코드 금지.
2. **밸런스 수치 하드코딩 금지**: 전부 `Data/Balance/BalanceConfigSO` 또는 해당 SO. 새 수치는 docs 표에도 추가.
3. **시스템→UI 직접 호출 금지**: EventBus 이벤트만. UI는 구독자다.
4. 스코프 가드레일 (docs/00) 위반 기능 제안 금지: 전투, 절차지형, 전용서버, 인벤토리 UI, PvP, 호스트 마이그레이션.
5. 파일 300줄 초과 시 분리 제안. 싱글톤은 docs/02 허용 목록만.

## 컨벤션

- 네임스페이스 = 폴더: `RatGame.Core/Net/Player/World/AI/Noise/Run/Meta/Data/UI` (docs/02)
- `[SerializeField] private` 기본, 공개 필드 최소화. RPC: `~ServerRpc`/`~ClientRpc` 접미사.
- 로그는 `Log.Dev()` 래퍼 (릴리즈 스트립). 주석은 '왜'만, 한국어 OK.
- .meta 파일을 직접 만들거나 수정하지 않는다 (에디터가 생성).

## 작업 산출 형식 (매 태스크)

1. 변경 파일 목록 + 핵심 결정 요약
2. **에디터 체크리스트** — 프리팹 생성·인스펙터 연결·씬 배치 등 사람이 할 일
3. 테스트 방법 (Multiplayer Play Mode 인원 수, 확인 항목 — 해당 docs 수용 기준 인용)

에디터 반복 작업은 `Scripts/Editor/`에 MenuItem 툴로 자동화하는 것을 우선 검토 (`Tools/RatGame/...`).

## 테스트

- 로컬: Multiplayer Play Mode (에디터 4인). 원격: 스팀 빌드 2대, AppID 480.
- 넷코드 코드를 수정했으면 반드시 MPPM 2인 이상 테스트 방법을 제시할 것.
- 물리 운반의 어색함은 웃기면 사양이다 — '정확도 개선' 리팩토링을 자발적으로 하지 않는다 (docs/00 필러 1).

## 빌드

- 타깃: Windows x64 (데모 기준). Boot 씬이 빌드 인덱스 0.
- Facepunch 초기화 실패 시에도 UnityTransport 모드로 실행 가능해야 한다 (docs/03).

## 로컬 환경 메모 (이 머신 한정 — 팩 원본과의 차이)

- 프로젝트 폴더는 `~/project/game/Rat` (폴더명 변경 금지 — Claude 대화 기록이 경로에 묶임). 게임 이름과 무관.
- Unity **6000.3.10f1** (팩 기준 6000.0보다 최신 패치 — 이상 없으면 그대로 간다). macOS 개발, 빌드 타깃은 Windows x64.
- Unity MCP 브리지: CoplayDev `com.coplaydev.unity-mcp`, HTTP `127.0.0.1:8080/mcp`, `.mcp.json` 등록됨.
  씬·프리팹·패키지 조작과 콘솔 확인은 MCP로 직접 한다. 안 되는 것만 에디터 체크리스트로.
- `.claude/` 에이전트 42개·스킬 37개 활성 — `.claude/docs/quick-start.md` 참조.
- 세션 로그: `production/session-logs/session-log.md`
