# 고양이 10 — 댕청한 실패 (2026-09-24, 세트 2의 6단계 — 미끄러짐·깜짝·비틀거림)

## 목표
무서운 고양이가 가끔 바보가 된다 — 긴장을 푸는 밸브, 직후 다시 무서워짐(대비). 실패는 **쥐가 만든 상황**에서만 확실히 일어난다(무작위 실패 금지). (design/cat-ideas/11)

## 설계
- `CatState.Blunder` + `BlunderKind` NV(클라 연출): `AI/CatBrain.Blunder.cs`.
- **미끄러짐(Slip)**: 고양이가 속도 ≥4(추격·호기심)로 달리다 바닥에 풀린 Slippery 물건(비누·접시·수박) 0.7m 안을 밟으면 → 진행 방향으로 3m를 0.6s에 걸쳐 쭉(NavMeshAgent.Move — NavMesh 밖으로는 안 나감, 벽·상자 모서리에서 멈춤). 밟은 물건도 앞으로 튕김.
  - 도중에 막히면(한 프레임 이동량 < 요청의 50%) **쿵 → 기절(Stun) 2s**. 안 막히면 0.8s 추스르기. 끝나면 Return(보이면 곧 다시 추격). 쿨다운 4s.
- **깜짝(Startle)**: 호기심으로 노는 중 2.5m 안에서 깨짐·함정·찍찍(즉시 조사 소음) → 뒤로 1.2m 펄쩍 1.5s → 소리 난 곳 조사(Suspicious). "자기 발에 떨어뜨려 놀람".
- **비틀거림(Wobble)**: 캣닢(CatLure Catnip) 놀이가 끝나면 10s 동안 비틀비틀 — 1s마다 주변 2m 랜덤 지점으로 속도 ×0.5, 감각 ×0.3(취함). 목격 전이는 그대로(코앞이면 쫓는다).
- 실패는 앙심을 올리지 않는다(스스로 한 실수). 비누 놓은 쥐 +10은 귀속 문제(충돌 소음과 같은 Source 0) 해결 뒤.
- CatVisual: Slip = 몸 옆으로 기울어짐, Stun = 회청색 + 빙글 흔들, Startle = 몸이 위로 튐, Wobble = 좌우로 휘청.
- 수치 (BalanceConfig "실패"): catSlipMinSpeed 4 · catSlipDetectRadius 0.7 · catSlipDistance 3 · catSlipSeconds 0.6 · catSlipStunSeconds 2 · catSlipRecoverSeconds 0.8 · catSlipCooldown 4 · catStartleRadius 2.5 · catStartleSeconds 1.5 · catWobbleSeconds 10 · catWobbleSpeedMul 0.5.
- 다음: 헤어볼(그루밍 뒤 20%, Slippery 전리품 + 도감), 점프 실패·상자 끼임(선반·상자 환경이 생기면).

## 검증
- 추격 경로에 비누 → Slip 로그·이동 거리 ≈3m 또는 벽 앞 Stun 2s.
- 호기심 중 옆에서 Break 소음 → Startle → Suspicious.
- 캣닢 Distracted 끝 → Wobble 10s(속도 ≈1.0) → Return.

## 검증 결과 (2026-09-24)
- 미끄러짐(열린 바닥): 사냥꾼이 추격 중 속도 5.4로 비누 밟음 → 3.03m 미끄러짐 → 추스르기 → Return.
- 미끄러짐(상자 앞): 1.91m 미끄러지다 상자 모서리(NavMesh 경계)에 막힘 → "쿵!" 기절 2.0s → Return.
- 깜짝: 치즈 놀이 중 1.5m 옆 깨짐 소음 → Startle 1.3s → Suspicious.
- 비틀거림: 캣닢(ServerDistract wobbleAfter) 2s 뒤 Wobble 10s, 안정 속도 1.00(순찰 2 × 0.5) → Return.
- 2인: 빌드 클라 로그 "고양이 실패 연출: Slip" → "Stun"(BlunderKind 복제). 2인 세션의 시야 셋업이 불안정해 미끄러짐 시작은 리플렉션으로 직접 호출(감지는 1인에서 검증).
- 테스트 교훈: 앞 단계에서 다운된 쥐를 상태만 Active로 돌리면 고양이가 못 보는 경우가 있어, 셋업마다 플레이 모드를 새로 시작했다.
- 미검증: 밟은 비누가 앞으로 튕기는 모습(화면), 호기심 속도 4.0에서의 미끄러짐.
