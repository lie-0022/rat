# 고양이 32 — 집주인 이벤트: 불 켜짐 (2026-09-24, design/cat-ideas/10)

## 목표
발소리 3s 예고 → 어두운 방 하나에 30s 불이 켜진다(어둠 은신 붕괴). 고양이는 그 방으로 와서 집주인 다리에 10s 부빈다(정지·감각 0.5). 예고를 들으면 **그 방에서 나오기**가 정답 — 고양이도 곧 그리로 온다.

## 설계
- `HouseEventKind.LightOn`(enum 끝) — 스케줄 순번 합류(LightZone이 맵에 있을 때). 토스트: 예고 "쿵쿵 — 집주인 발소리, 어두운 방에 불이 켜진다", 시작 "딸깍 — 불 켜짐! 어둠이 사라졌다", 끝 "불 꺼짐 — 다시 어둠".
- `LightZone.ServerLightFor(seconds)`: Lit 켜고 시간이 끝나면 끄고 End 알림. 어느 방인지는 무작위(꺼진 구역 중).
- 고양이: TV와 같은 "가서 앉기"를 일반화 — `ServerGoSit(point, face?, giveUpAt, dwell, sense)`. TV = (시청 지점, TV, +90s, 끝까지, 1). 불 켜짐 = (구역 중심, 없음, +30s, 10s, 0.5 — 부비느라 둔함).
- 수치: lightOnSeconds 30 · lightOnGreetSeconds 10 · lightOnGreetSense 0.5.

## 검증
- 트리거 → 3s → Lit true, IsDark(구역 중심) false, 고양이 구역 중심 도착 → 10s 머무름 → 순찰 복귀.
- 불 켜진 구역 속 6m 쥐 → 보임(어둠 해제 확인은 31에서 이미 — 여기선 IsDark만).
- 30s 뒤 Lit false, End 토스트.
- 2인: 클라 Lit 복제·토스트 3종.

## 검증 결과 (2026-09-24)
- 트리거 → 예고 3.0s 뒤 Lit true, IsDark(구역 중심) false. 고양이 8.2s 뒤 중심 0.60m에 앉음(감각 0.5), 부비기 10.0s 뒤 Patrol, 불은 계속 켜짐.
- 30s 뒤(timeScale 6) 다시 어둠, End 토스트.
- 회귀: TV를 "가서 앉기" 일반화(ServerGoSit)로 옮긴 뒤 TV 시청 지점 0.60m 도착 확인.
- 2인: 빌드 클라 토스트 3종 + "어둠 연출: 불 켜짐/어둠", 예외 0.
- 미검증: 실플레이에서 예고 3s가 방을 빠져나오기에 충분한지(어둠 구역 8×8 — 가운데서 4m, 달리기면 1s 안).
