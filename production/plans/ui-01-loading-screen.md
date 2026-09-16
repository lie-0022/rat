# 계획 1 — 로딩 화면 (기지 → 스테이지, 스테이지 → 기지)

> **완료 (2026-09-16)** — 계획대로 구현. 검증: 에디터 호스트 1인 출발·귀환(스크린샷 `Captures/loading_depart.png`·`loading_return.png`), 에디터 호스트 + macOS 빌드 클라 2인 동시 출발(양쪽 로그 켬→전원 완료에 끔, 호스트 "기다리는 중" 표시). 곁다리 수정: `DeparturePad.Update`가 스폰 전 NetworkVariable을 쓰던 경고.

## 목표
씬이 바뀌는 동안 검은 화면·멈춘 화면 대신 **팁 문구가 있는 로딩 오버레이**를 보여준다. 와이어프레임 6화면 중 마지막 미구현 (docs/12 화면 플로우 `Hub ── 레버 ──▶ 로딩(팁 문구) ──▶ InRun HUD`).

## 사양 근거
- docs/12 화면 플로우, 와이어프레임 결정(2026-09-15): 로딩 = 팁 문구 화면.
- docs/03 씬 동기화: 호스트 `NetworkManager.SceneManager.LoadScene(..., Single)` → 클라 자동 로드. 로드 완료 = `OnLoadEventCompleted`.
- docs/09·11: 출발 = `DeparturePad.Depart()` → 스테이지 로드 → `RunManager.OnStageLoaded`(전원 완료) → 순간이동 + 자동 출발. 복귀 = `RunManager.LeaveStage()` → 기지 로드 → `DeparturePad.OnBaseLoaded`.

## 현재 코드
- Hud 프리팹은 소유 플레이어 스폰 시 생성·`DontDestroyOnLoad` (`UI/CarryHud`) → 씬 전환을 살아남으므로 **로딩 오버레이를 Hud 안에 두면 된다.**
- 씬 이벤트를 UI가 구독하는 코드는 아직 없음. NGO의 `NetworkSceneManager.OnSceneEvent`는 클라에서도 온다 (`SceneEventType.Load` 시작, `LoadComplete`(자기), `LoadEventCompleted`(전원)).
- 출발 카운트다운은 `DeparturePad.Counting/DepartAt` NetworkVariable, RunBar가 이미 읽어 "출발 N초"를 그린다.

## 설계
### 표시 시점 (로컬, 각 클라 독립)
1. **켜기**: `OnSceneEvent`에서 `SceneEventType.Load`(내 클라가 로드 시작) 받으면 오버레이 ON. 어느 씬이든 상관없이 Single 로드면 전부 (기지→스테이지, 스테이지→기지, 메뉴→기지 첫 입장도 포함 — 첫 입장은 Hud가 아직 없을 수 있으니 자연히 안 뜸, OK).
2. **끄기**: `SceneEventType.LoadEventCompleted`(전원 로드 완료) 받으면 페이드 아웃. 전원 완료를 기다리는 이유: 스테이지에서는 그 시점에 순간이동+출발이 일어나므로, 그 전에 끄면 "남이 로딩 중인 동안 멈춘 화면"이 보인다. 안전장치: `Load` 후 `_maxSeconds`(BalanceConfig 아님 — UI 상수 15s) 지나면 강제 OFF.
3. 결과 화면 → 기지 복귀도 동일 경로라 자동 적용.

### 내용
- 전체 화면 `Panel` 색(불투명) 위에:
  - 큰 제목: 목적지별 — 스테이지로 갈 때 "창고로 출발…", 기지로 갈 때 "기지로 돌아가는 중…". 목적지는 `SceneEvent.SceneName`으로 판단 (`Hub` = 기지, 그 외 = 스테이지).
  - 팁 문구 1줄 (AccentText·Body): `Data/UI/LoadingTips.asset` (`ScriptableObject` `LoadingTipsSO { string[] Tips }`)에서 랜덤. 초기 팁 8개는 docs/00·04·05·07의 규칙에서 뽑는다 (예: "웅크리면 발소음이 0 — 고양이 옆에서는 기어가자", "계란은 던지면 깨진다. 굴려서 넣자", "쥐구멍에 넣은 건 안전 — 전멸해도 잃지 않는 건 누계뿐", "여럿이 들면 무거운 것도 움직인다", "어두운 곳에선 고양이 시야가 절반", "쓰러진 동료는 쥐구멍까지 옮기면 살아난다", "끈끈이에 붙은 동료는 [E] 길게 눌러 구출", "직선 달리기는 고양이보다 빠르다 — 막다른 길만 피하자").
  - 아래: 진행 표시. NGO는 로드 진행률을 주지 않으므로 **점 3개 깜빡임 텍스트("…")**로 끝. 진행 바 만들지 말 것(가짜 진행률 금지).
  - 인원 대기 표시(선택): `LoadComplete`(자기) 이후 `LoadEventCompleted` 전이면 "친구들을 기다리는 중…" 소문구.
- 페이드: 켤 때 즉시, 끌 때 `CanvasGroup.alpha` 0.4s.

### 파일
- 신규 `UI/LoadingOverlayWidget.cs` (Hud 안 `LoadingArea` 오브젝트, 항상 켜짐. 자식 `Panel`을 켜고 끔). `OnEnable`에서 `NetworkManager.Singleton.SceneManager.OnSceneEvent` 구독 — SceneManager는 세션 시작 뒤에만 존재하므로 **Hud 생성 시점(스폰 후)엔 있음**. `OnDisable`/`OnDestroy` 해제. `NetworkManager`가 null이면 무시.
- 신규 `Data/LoadingTipsSO.cs` + `Data/UI/LoadingTips.asset` (Editor MenuItem 불필요 — execute_code로 생성).
- Hud 프리팹: `LoadingArea` 추가. **정렬**: 결과 화면·일시정지보다 위여야 하므로 하위 캔버스 `overrideSorting`, sortingOrder 40 (PauseMenu 30 위). 일시정지 창이 로딩 중 열리는 건 막지 않아도 됨(뒤에 가려짐).
- `HudController`: 로딩 중엔 조준·슬롯 그룹을 숨길 필요 없음(오버레이가 불투명).

### 하지 않는 것
- 로드 진행률 바, 씬별 배경 이미지(아트 단계), 최소 표시 시간 강제(로드가 빠르면 그냥 짧게 뜬다).

## 구현 순서
1. `LoadingTipsSO` + 에셋(팁 8개).
2. `LoadingOverlayWidget` — 이벤트 구독·표시·페이드·안전 타임아웃.
3. Hud 프리팹에 영역 추가(execute_code, 테마 역할).
4. 검증 → docs/12 구현 현황 + 이 파일 상단에 완료 표기.

## 에디터 체크리스트
- 없음 (전부 execute_code로 프리팹·에셋 생성). 팁 문구 수정은 `Data/UI/LoadingTips.asset` 인스펙터.

## 테스트
- 에디터 호스트 1인: Hub에서 발판 집합 → 카운트다운 → 로딩 오버레이 ON(제목 "창고로 출발…", 팁 표시) → 스테이지 도착 후 OFF·페이드. 귀환/전멸 → 결과 화면 → "기지로 돌아가는 중…" → 기지에서 OFF. 스크린샷 각 1장.
- 2인(에디터 + 빌드 `-autojoin`): 클라 화면에도 뜨는지, `LoadEventCompleted` 전까지 유지되는지(호스트가 먼저 끝나도 클라 완료까지 호스트 오버레이가 남는지) 로그로 확인.
- 안전장치: 강제로 한쪽이 15s 이상 못 끝내는 상황은 만들기 어려움 — 타임아웃 코드는 리뷰로만.
- 반복: 출발→귀환→출발 2회 돌려 오버레이가 남거나 중복 구독되지 않는지 (`_open` 상태 로그).

## 열린 질문
- 팁을 `Data/UI/`에 둘지 `Data/Text/`로 뺄지 — 번역(LocalizationTable) 작업 때 함께 옮기면 되므로 지금은 `Data/UI/`.
