# 고양이 14 — 관심 표시 + 초인종 (2026-09-24, design/cat-ideas/13의 가벼운 조각)

## 목표
13 관심 생태계의 전면 통합(Curious·Distracted → Attend)은 회귀 위험이 커서 뒤로 미루고, 지금 바로 플레이에 보이는 두 조각만:
1. **관심 표시**: 고양이가 놀고 있을 때(Curious·Distracted — 유인이 먹히는 중) HUD에 초록 "냥"(폰트에 ♪ 없음). 필러 2(읽히는 위험) — "지금 캔에 꽂혔다, 가자".
2. **초인종**: 맵 오브젝트. 쥐가 E 1s 홀드로 누르면(런당 1회) "딩동!" → 집주인이 현관으로 → 고양이도 따라 나간다(부르기와 같은 부재). 관심 축의 최상위 카드 — 쥐가 **이벤트를 유발**.

## 설계
- `SuspicionIndicatorWidget`: 우선순위 나를 쫓음 "!"/"!!" > 의심 "?"+게이지 > **관심 "냥"**(가장 가까운 Curious·Distracted 고양이, 15m 안, 게이지 없음, 색 Positive). Jua 폰트에 ♪가 없어 "냥".
- `World/Doorbell` (NetworkBehaviour + IInteractable): HoldSeconds 1, `Used` NV(런당 1회 — 클라 프롬프트도 사라짐), 호스트에서 `HouseEventDirector.ServerTrigger(Doorbell)`. 
- `HouseEventKind.Doorbell`(enum 끝): 부르기와 같은 고양이 반응(부재 20~40s), 토스트 예고 "딩동! 누가 초인종을…" / 시작 "집주인이 현관으로 — 고양이도 따라갔다!". 스케줄 카운트는 안 씀.
- 데모 레이아웃: 초인종 1개(서쪽 문 옆 -18,0.4,-2).
- 수치: doorbellHoldSeconds 1 · attentionIndicatorRange 15.

## 검증
- 캔 굴려 Curious → 내 HUD "♪" → 끝나면 사라짐. 의심이 생기면 "?"가 우선.
- 초인종 E 홀드 → 예고 토스트 → 3s 뒤 고양이 문으로 → 부재. 두 번째 시도 불가(프롬프트 없음).
- 2인: 클라가 초인종 누름(홀드 → ServerRpc) → 호스트 고양이 부재, 클라 토스트.

## 검증 결과 (2026-09-24)
- 관심 표시: 치즈 굴려 Curious → HUD 초록 원 "냥" 표시(스크린샷 — 고양이 머리 위) → 놀이 끝(Return) → 사라짐. Jua 폰트에 ♪ 없음 확인(HasCharacter false) → "냥".
- 초인종(1인): 프롬프트 "초인종 누르기"·홀드 1s → 누름 → 예고 토스트 → 3.0s 뒤 고양이 Away → Used true·CanInteract false(두 번째 불가)·스케줄 몫(remaining) 그대로.
- 2인: 클라(1)가 누른 것으로 호스트에서 실행 → 빌드 클라 로그 토스트 "딩동! 누가 초인종을…" → "집주인이 현관으로 — 고양이도 따라갔다!", 씬 NetworkObject 동기화 에러 없음.
- 미검증: 실제 E 키 홀드 입력(상호작용 파이프라인은 기존 Mirror·HideSpot과 같음).
- 다음(13 본체): Curious·Distracted → Attend 통합 + 레이저 포인터·흔들리는 끈 — 회귀 위험이 커서 별도 단계.
