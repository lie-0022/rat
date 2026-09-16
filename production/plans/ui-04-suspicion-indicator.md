# 계획 4 — 의심 표시 (나를 노리는 고양이 방향 + ?/!)

> **완료 (2026-09-16)** — 계획대로. 바꾼 점: 게이지는 마커 위(아래는 뒤쪽 화살표와 겹침), 화살표는 흰 마름모(기본 드롭다운 스프라이트는 회색이라 색이 탁함), `ScreenAnchor`의 카메라 뒤 방향을 나침반식으로(바로 뒤에서 방향이 흔들렸음 — 핑 마커도 같이 좋아짐). 스테이지 씬엔 아직 고양이가 없어 **테스트 때만 런타임 스폰**(씬은 안 건드림). 검증: 에디터 호스트 1인 — 실제 소음 → Suspicious → 머리 위 "?"+게이지 40%, 뒤돌면 아래쪽 원 위+마름모, 고양이가 4m에서 보게 → Chase(나) → 빨간 "!", 뒤·옆에서 원 위 표시. 에디터 호스트 + macOS 빌드 클라 2인 — 고양이가 클라를 추격하면 호스트 표시 없음, 클라 로그 "! (Chase)", 포획 후 없음. 콘솔 에러 없음.

## 목표
docs/12 `SuspicionIndicator — 나를 의심 중인 고양이 방향 화살표 + ?/!`, docs/07 "게이지는 NetworkVariable로 복제 → 타깃 플레이어 HUD에 ?/! 표시". 고양이 작업 때로 미뤘던 항목이지만 **필요한 NetworkVariable은 이미 있어** UI만 붙이면 된다.

## 사양 근거
- docs/07: `CatState { Sleep, Patrol, Suspicious, Chase, Capture, Distracted, Return }`. Patrol →(게이지 ≥ CatSuspicionThreshold 30)→ Suspicious →(≥ CatChaseThreshold 100 or 직접목격 1s)→ Chase. 게이지 자극 없으면 -20/s.
- docs/03 NetworkVariable 표: `CatBrain.State, TargetClientId`. 코드에는 `CatSenses.SuspicionGauge`(float NV)도 있음 — **docs/03 표에 추가할 것**(이미 복제 중인데 표에 빠짐).

## 현재 코드
- `AI/CatBrain`: `State` NV, `TargetClientId` NV (Chase 중 타깃, 아니면 0). Suspicious 단계에서는 TargetClientId가 0일 수 있음(의심은 "지점"에 대한 것).
- `AI/CatSenses`: `SuspicionGauge` NV(0~CatChaseThreshold), `VisibleTarget`(호스트 전용, 복제 안 됨).
- HUD에 고양이 관련 위젯 없음. 고양이 프리팹·씬 배치는 Stage_Warehouse01에 있는지 확인 필요(없으면 `CatTools` MenuItem로 스폰 가능한지 확인).

## 설계
### 무엇을 누구에게
- 각 클라는 씬의 모든 `CatBrain`을 0.2s마다 찾아(4마리 이하) 다음을 판정:
  - **Chase**이고 `TargetClientId == 내 Id` → "!" (Danger) + 방향 화살표. 최우선.
  - **Suspicious** (누구든) → "?" (Warning) + 방향 화살표. 타깃 정보가 없으므로 **가장 가까운 Suspicious 고양이** 하나만. 게이지 비율(`SuspicionGauge / CatChaseThreshold`)을 "?" 아래 작은 링/막대로 — 30→100 사이가 차오르는 게 보이면 "지금 숨어야 한다"가 읽힌다.
  - Chase인데 타깃이 남이면 → 표시 없음(팀 상태/토스트 담당 아님 — 후속: 동료가 쫓기면 토스트 "파랑 쥐가 쫓기고 있어요"는 이 태스크 범위 밖).
  - 그 외 → 숨김.
- 방향: 고양이 위치 → 내 카메라 기준 **수평 각도**. 화면 안(시야각 안, 화면 좌표 안)이면 고양이 머리 위에 오버레이 마커(핑 마커와 같은 WorldToScreen 방식). 화면 밖이면 화면 중앙 반경 180px 원 위에 각도대로 배치한 화살표 + ?/! — 오버레이 캔버스 하나로 둘 다 처리.
- 소리는 없음(오디오 단계). 표시 자체는 소음/감지에 영향 없음.

### 파일
- 신규 `UI/SuspicionIndicatorWidget.cs` (Hud `SuspicionArea`, 항상 켜짐): `[SerializeField] UiThemeSO _theme, BalanceConfigSO _balance, RectTransform _marker(화살표+글자 묶음), Image _arrow, TMP_Text _symbol, Image _gaugeFill`. `Camera.main` 사용, 내 clientId = `NetworkManager.LocalClientId`. 폴링 0.2s로 대상 선정, 위치 갱신은 매 프레임.
- Hud 프리팹 `SuspicionArea` (Aim 그룹 밖 — UI 패널 열려도 보이게, 결과 화면 중엔 숨김: `RunManager.IsShowingResult` 읽기).
- 핑 마커(계획 3)와 "월드 → 화면 위치·가장자리 클램프" 로직이 겹침 → **`UI/ScreenAnchor` 정적 헬퍼**(WorldToScreen, 화면 밖 판정, 원 위 배치)로 공유. 계획 3을 먼저 하면 거기서 만들고 여기서 재사용.
- docs/03 표에 `CatSenses.SuspicionGauge` 추가, docs/07·12 구현 현황.
- BalanceConfig 신규 수치 없음(임계값 재사용). UI 상수: 원 반경 180px, 폴링 0.2s.

### 하지 않는 것
- 고양이 AI 수정, 동료 피추격 토스트, 화면 가장자리 붉은 비네트(연출 — 아트/후속), 소리.

## 구현 순서
1. (계획 3 선행 시) `ScreenAnchor` 헬퍼 재사용, 아니면 여기서 신규.
2. `SuspicionIndicatorWidget` + Hud 영역.
3. 검증 → docs 갱신.

## 에디터 체크리스트
- 스테이지 씬에 고양이가 없으면 `Tools/RatGame/Cat…` 메뉴(CatTools) 또는 프리팹 배치 — 구현 세션이 씬 상태를 먼저 확인.

## 테스트
- 에디터 호스트 1인, 고양이 1마리: execute_code로 `CatSenses.SuspicionGauge`를 서버에서 40 → "?" + 게이지 막대 40%. 100 → CatBrain이 Chase로 전이·`TargetClientId`=0(호스트) → "!"(빨강). 고양이가 등 뒤(카메라 밖) → 원 위 화살표가 뒤쪽. 정면 → 고양이 머리 위 마커. 스크린샷 3장.
- 2인(에디터 + 빌드): 고양이가 클라(빌드)를 쫓게 만든 뒤(`_chaseTarget`을 클라로 — 서버 execute_code) 호스트 화면엔 "!" 없음, 클라 로그에서 위젯 상태 "!" 확인.
- 게이지 감쇠: 자극 없이 5s → "?" 사라짐.
