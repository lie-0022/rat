# 자율 개발 루프 상태 (매 틱 시작 때 읽고, 단계가 바뀔 때마다 갱신)

> 목적: 턴이 실패(도구 호출 파싱 실패·API 오류)하거나 컨텍스트가 요약돼도 다음 틱이 여기서 이어받는다.
> 루프 지시(사용자, 2026-09-24): "최대한 개발할 수 있는 거 많이 기획하고 하나씩 검증하면서 개발해놔. 내가 와서 그만하라고 할 때까지 절대 그만하지 말고 계속 개발 기록하면서"

## 루프 구동 방식 (2026-09-24 개편)
- 주 구동: `CronCreate` 반복 `*/5 * * * *` — 턴이 실패해도 5분 안에 다음 틱이 시작된다 (세션 전용, 7일 만료).
- `ScheduleWakeup` 사슬만 쓰지 않는다: 턴이 실패하면 예약이 안 걸려 루프가 죽는다 (2026-09-24 04:20 KST 실제 발생, 8.5시간 정지).
- 틱 규칙: ① 이 파일 읽기 → ② `git status`로 미완 작업 확인 → ③ 한 단계 진행 → ④ 이 파일 갱신 → ⑤ 기능 하나가 검증되면 커밋.
- 멈춤: 사용자가 "그만"이라고 하면 `CronDelete`.

## 현재 작업: 고양이 5 — 숨을 곳 + 수색 (design/cat-ideas/14)
상태: **코드 작성 완료, 컴파일 미확인** (커밋 안 됨)

완료
- `Player/PlayerCondition` — `ConditionState.Hidden` (enum 끝에 추가)
- `World/HideSpot.cs` — IInteractable, 들어가기 홀드 0.3s / 나오기 즉시 + 소음 10, 정원, 대형 끌면 불가, `ServerEject()`
- `AI/CatBrain.Search.cs` (partial) — `CatState.Search`: 목격점 반경 6m 스팟 가까운 순 ≤3, 킁킁 2s(여럿이면 ×인원×1.5), 30% 건드림 → 발각 → Pounce 창 0.5s → Chase
- `AI/CatBrain.cs` — partial화, Search 틱/진입, 추격 타깃이 Hidden이면 Return 대신 시야 상실 처리, 시야 상실 3s 뒤 `TryEnterSearch` 우선
- `Data/BalanceConfigSO` — 숨을 곳·수색 수치 9개
- `AI/CatVisual` — Search 연노랑 + 코 킁킁 모션
- `UI/TeamStatusRow` "숨음" 칩, `UI/HiddenOverlayWidget.cs`(어두운 화면 + 심장 박동, 고양이 거리 비례), `HudController._hiddenOverlay` 바인딩
- `Editor/CatTools.BuildDemoLayout` — Hide_Shoe(3,0.3,-8) 1인·Hide_Box(-3.5,0.6,7) 2인 (1차 컴파일 에러 = 블록이 엉뚱한 메서드에 들어감 → 옮김)

남은 단계
1. 컴파일 확인 (에디터 isPlaying=false, 7777 비었는지 먼저)
2. Hud 프리팹에 HiddenArea(CanvasGroup·Heart·Text) 만들고 `_hiddenOverlay` 연결
3. 데모 레이아웃 툴 실행 → 씬 저장 (HideSpot NetworkObject 확인)
4. 플레이 검증: 숨기(E 홀드) → Hidden·시야 제외 / 추격 중 숨기 → Search 진입 → 킁킁 로그 → DisturbChance 1로 임시 설정해 발각·Pounce·Chase / 나오기 소음 / 대형 끌며 숨기 불가
5. MPPM 또는 빌드 2인: 클라가 숨고 나오기 (텔레포트 RPC가 소유자에게 가는지)
6. docs/07(FSM 표 Search 행)·docs/04(Hidden)·docs/12(HiddenOverlay)·docs/10(방마다 HideSpot 1~2) 갱신, `production/plans/cat-05-hide-search.md`, 세션 로그, 커밋

## 다음 후보 (세트 2)
- 배고픔·호기심 앞발 (design/cat-ideas/03), 관심 경제, docs/07·10 정리
- 보류(사용자 결정 필요): 두 축 감각, 선반 층, 고양이 스태미나
