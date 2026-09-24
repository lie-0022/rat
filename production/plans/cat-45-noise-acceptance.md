# 고양이 45 — docs/06 소음 수용 기준 검증 + 파문 이펙트 (2026-09-24)

## 발견
- docs/06의 "loudness 40 이상이면 파문 이펙트 ClientRpc"가 **구현 안 됨**(밸런스에 rippleThreshold 40만 있었다).
- 벽 감쇠는 NoiseBlocker 레이어만 보는데, 고양이 40에서 만든 창고방 벽이 RoomStatic이라 **소리가 벽을 그냥 통과**했다.

## 한 것
- 파문: 호스트 `NoiseSystem.Rippled`(≥ rippleThreshold) → `RunManager.NoiseRippleClientRpc` → `EventBus.NoiseRipple` → `Noise/NoiseRippleView`(자동 생성, LineRenderer 고리 0.7s, 반경 = 전파 반경, 8개 재사용). 연출만 — 규칙 3(시스템→연출은 EventBus).
- 창고방 벽·문판 → NoiseBlocker 레이어(시야도 계속 막힘, NavMesh 문틈 유지 확인).

## 검증 결과
- 40 소음: 창고방 벽 너머 24.0(×0.6), 열린 문 줄 40.0, 닫힌 문 24.0, 빈 바닥 대조 40.0.
- 파문: 발소리 22 → 0회, 접시 깨짐 60 → 1회·고리 활성(Sprites/Default)·고양이 Suspicious. 스크린샷으로 노란 고리 확인.
- 2인: 빌드 클라 "소음 파문 연출: 60" 1회(발소리는 없음), 예외 0.
- 남음: 소음 기즈모 눈 확인(Scene 뷰 — 사람).
