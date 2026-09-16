# UI 남은 작업 구현 계획 (2026-09-16)

docs/12 와이어프레임 이후 남은 UI 5건. 계획만 여기에 두고 구현은 별도 세션에서 한 건씩 한다.
구현 세션은 해당 계획 파일 + 계획이 가리키는 docs 절을 읽고 시작한다. 계획과 docs가 어긋나면 **docs가 정답** — 계획을 고친다.

| # | 계획 | 다른 시스템 의존 | 권장 순서 |
|---|---|---|---|
| 1 | [로딩 화면](ui-01-loading-screen.md) | 없음 | **완료 (2026-09-16)** |
| 2 | [CarryInfo](ui-02-carry-info.md) | 없음 (아이콘 스프라이트는 아트 단계) | 2 |
| 3 | [핑 마커](ui-03-ping-marker.md) | 핑 입력·RPC 신규 (계획에 포함) | 3 |
| 4 | [의심 표시](ui-04-suspicion-indicator.md) | 고양이 AI (CatBrain·CatSenses 이미 있음) | 4 |
| 5 | [기지 로비 UI](ui-05-lobby-ui.md) | **Facepunch(태스크 0-6) 선행 필수** | 5 (0-6 뒤) |

공통 규칙 (모든 계획에 적용):
- uGUI + TMP, `Data/UI/UiTheme.asset` 역할만 사용(`ThemedGraphic`), 런타임 색은 `_theme.GetColor`. 새 색 역할은 enum 끝에만.
- HUD 위젯은 `Prefabs/UI/Hud.prefab` 안, 스크립트는 항상 켜진 부모에 두고 표시 오브젝트만 켜고 끈다.
- 시스템→UI 호출 금지. 위젯은 NetworkVariable/컴포넌트 상태를 읽거나 EventBus(로컬)만 구독.
- 새 수치는 `BalanceConfigSO` + docs 표. 넷코드 변경 시 docs/03 NetworkVariable 표 갱신.
- 검증은 실제 입력 주입·스크린샷·수치로. 넷코드 건드리면 2인 이상(에디터 호스트 + macOS 개발 빌드 `-unitytransport -autojoin`, 또는 MPPM).
- 끝나면 docs/12 구현 현황에 기록, 이 README의 행을 "완료 (커밋)"로 바꾼다.
