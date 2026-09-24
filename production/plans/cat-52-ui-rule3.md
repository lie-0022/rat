# 고양이 52 — docs/12 "UI 직접 참조 0건" 검증 → 기지 패널 3건 규칙 3 정리 (2026-09-24)

## 발견
게임플레이 폴더(AI·Core·Data·Meta·Net·Noise·Player·Run·World)에서 `RatGame.UI`를 찾으니 World의 자판기·거울·도감이 패널 프리팹(ShopPanel·MirrorPanel·CodexPanel)을 **직접 Instantiate·Open·ShowMessage**하고 있었다(규칙 3 위반). Data의 LoadingTipsSO·UiThemeSO는 메뉴 경로 문자열·UI용 데이터라 문제없음.

## 한 것
- `EventBus`: `WorldPanelKind { Shop, Mirror, Codex }`, `WorldPanelRequested(kind, source)`, `WorldPanelSourceGone(source)`, `ShopPurchaseResult(ok, message)`.
- World 3개: 패널 필드 제거 → 대상 클라 ClientRpc에서 이벤트만 올림, 디스폰 때 SourceGone. CodexBook은 `Database` 프로퍼티로 목록 제공.
- `UI/WorldPanelHost`(Hud 프리팹 루트): 패널 프리팹 3개 보유, 요청에 생성·Open, 구매 결과 전달, 원천이 사라지면 그 패널 파괴.

## 검증
- 스테이지 → 기지(Hub) 씬 로드 → 자판기·거울·도감 ServerInteract → 세 패널 열림("패널 연출" 로그 3) → 없는 항목 구매 요청 → 결과 이벤트 "없는 항목" 도착 → 스테이지로 로드 → 세 패널 사라짐, 에러 0.
- 게임플레이 폴더 `using RatGame.UI` 0건.
- docs/12 나머지: 4인 TeamStatus·핑(4인 화면 확인 필요), 결과→Hub→재출발 UI 누수(패널 정리만 확인), 한/영 전환(번역 테이블 미구현 — SettingsService.Language 값만) — 미체크.
