# 고양이 2 — 잠의 단계 + 그레이박스 텔레그래프 (2026-09-24, 세트 1의 2단계)

## 목표
잠을 on/off가 아니라 **얕은 잠 ↔ 깊은 잠 파동**으로. 전환 3초 전 꼬리 씰룩 예고, 얕은 잠에서 자극은 "한쪽 눈 뜸"(3s) 뒤 다시 잠들거나 깸. 깊은 잠은 큰 자극(임계 ×2)만. 반지 플레이(잠자리 2m)의 타이밍 게임을 만든다. (design/cat-ideas/08)

## 설계
- `CatBrain.SleepPhase` NetworkVariable(None·Light·ToDeep·Deep·ToLight·HalfAwake). Light 12~20s(감각 0.5) → ToDeep 3s → Deep 10~18s(0.15) → ToLight 3s(예고) → Light… 총 잠 60s는 Light에서만 끝난다.
- HalfAwake: 자극 시 3s, 감각 0.5. 그 사이 목격/새 자극 → Suspicious, 없으면 이전 단계로.
- `AI/CatVisual`(전 클라, 읽기만): 몸 색(상태) + 꼬리 피벗 회전(Light 천천히 / ToLight 급씰룩 / Chase 빠름) + Deep 배 펄스·몸 가라앉음 + HalfAwake 머리 들림. Cat 프리팹에 TailPivot/Tail 큐브.
- 수치: BalanceConfig `고양이 잠의 단계` 헤더 8개.

## 검증
- 4배속 관찰: 단계 순서·지속 시간 로그, ToLight 예고가 Light 전에 반드시 옴.
- 얕은 잠 중 소음 40 → HalfAwake → 3s 뒤 Light 복귀 / 소음 뒤 쥐가 보이면 Suspicious.
- 깊은 잠 중 소음 40 → 반응 없음, 소음 80 → HalfAwake.
