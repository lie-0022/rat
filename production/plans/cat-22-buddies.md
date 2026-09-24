# 고양이 22 — 두 마리: 짝꿍 (협공 + 공동 수면) (2026-09-24, design/cat-ideas/07)

## 목표
관계가 판마다 달라지게: 2마리면 **앙숙**(고양이 15) 또는 **짝꿍**을 뽑는다.
- 짝꿍 **협공**: 한 마리가 추격을 시작하면 다른 마리(20m 안, 한가함)가 쥐의 **2초 뒤 예상 위치**로 돌아 들어간다(속도 4.0, 8s). "그럴듯하게만" — 정확한 포위는 도망을 불가능하게 한다. 보이면 평소대로 추격.
- 짝꿍 **공동 수면**: 한 마리가 잠자리에서 자기 시작하면 다른 마리가 같은 잠자리로 가서 같이 잔다(수면 ×1.5). 둘 다 자는 동안 집이 통째로 빈다 — 쥐의 황금기.
- 쥐의 틈: 협공은 무섭지만 **둘이 같은 곳에 있다**는 뜻 → 나머지 집이 빈다.

## 설계
- `CatRelation`: `Kind`(Rivals/Buddies) — 2마리가 처음 보일 때 `catBuddyChance` 0.5로 뽑음(`ServerSetKind`로 지정 가능). Rivals면 기존 싸움, Buddies면 싸움 없음 + `CatBrain.ServerStateChanged` 구독:
  - 누가 Chase 진입 → 다른 짝꿍이 Patrol·Return·Suspicious·Curious·Track·Search 중이고 20m 안이면 `ServerFlank(target)`.
  - 누가 Sleep 진입(잠자리) → 다른 짝꿍이 Patrol·Return 중이면 `ServerJoinSleep()`.
- `CatState.Flank`(enum 끝) — `AI/CatBrain.Buddy.cs`: 0.5s마다 타깃 위치 차분 속도로 2s 앞 지점(NavMesh 샘플)으로 이동 4.0. CheckEscalation(보이면 추격). 8s 또는 타깃 Active 아님 → Return.
- `ServerJoinSleep`: Patrol로 두고 가장 가까운 Bed 스팟 강제 → 도착해 Sleep → 잠 길이 ×1.5(1회).
- 수치: catBuddyChance 0.5 · catFlankRange 20 · catFlankLeadSeconds 2 · catFlankSeconds 8 · catFlankSpeed 4 · catBuddySleepMul 1.5.

## 검증
- 짝꿍 지정 → 고양이 A가 쥐 추격 → B가 Flank(목적지 = 쥐 앞) → 8s 또는 목격 → 추격.
- A 잠 → B가 잠자리로 가서 Sleep, 잠 길이 ×1.5.
- 짝꿍은 마주쳐도 안 싸움. 앙숙은 기존대로.
- 2인: Flank 상태 복제(연출 로그).

## 검증 결과 (2026-09-24)
- 런타임 2번째 고양이 + `ServerSetKind(Buddies)`. 고양이 A가 +x로 걷는 쥐를 추격 → 1.0s 만에 B가 Flank. B 목적지 x 14.4, 쥐 x 6.3(진행 방향 앞) ✓. 8s 뒤 Return/Suspicious.
- 공동 수면: A를 Sleep → B가 4.9s 만에 잠자리 도착·Sleep, 잠 길이 45s(사냥꾼 60×0.5=30 ×1.5) ✓.
- 짝꿍은 3.5m 마주 보게 둬도 싸움 0.
- 2인: 빌드 클라 로그 "고양이 협공 연출: Cat", 예외 0.
- 미검증: 관계 50% 추첨 분포, 짝꿍 협공이 실제로 포위해 잡는 빈도(밸런스 — 너무 세면 lead 2s·속도 4 조정).
