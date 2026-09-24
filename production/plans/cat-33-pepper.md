# 고양이 33 — 후추통 (2026-09-24, design/cat-ideas/06 "후추(아이템)가 마크를 지운다")

## 목표
냄새 자국을 끊는 **쥐 쪽 도구**. 물웅덩이(양날 — 젖은 발자국이 남는다)와 달리 후추는 깨끗하게 지우지만 **한 번뿐**이고, 고양이가 그 위를 지나면 재채기로 멈춘다 — 추적·추격 끊기.

## 설계
- `World/PepperShaker` (NetworkBehaviour, 들고 던지는 작은 원통 — CarryableItem): 던져서 착지하거나(IsRecentlyThrown 충돌) 세게 떨어지면(상대 속도 ≥ `pepperSpillImpactSpeed` 4) **쏟아진다** — 한 번뿐.
- 쏟아진 자리(바닥) 반경 `pepperRadius` 2m, `pepperSeconds` 45s 동안:
  - 그 안 냄새 자국을 매 프레임 지운다(물웅덩이와 같은 ScentSystem.EraseInRadius). 쥐는 젖지 않는다.
  - 들어온 고양이는 **재채기**(CatBlunderKind.Sneeze, 끝에 추가) `pepperSneezeSeconds` 2s 정지 → 자극 소비·Return(추적·추격 끊김). 같은 고양이는 `pepperSneezeCooldown` 8s 동안 다시 안 함.
- 연출: `PatchActive` NV → 바닥의 짙은 갈색 원판(모든 클라). 재채기 = CatVisual 몸 들썩(헤어볼과 비슷, 더 빠르게) + dev 로그 "재채기 연출".
- 데모 레이아웃: 후추통 1개 (-6, 0.3, -14) — 쥐구멍 쪽.
- 수치 5개 BalanceConfig "후추" 헤더.

## 검증
- 3m 높이에서 떨어뜨리면 쏟아짐(PatchActive), 0.3m에서 살짝 놓으면 안 쏟아짐.
- 패치 안 자국 지워짐(ScentSystem 개수), 쥐 젖음 없음.
- 추격 중인 고양이가 패치를 지나면 재채기 2s → Return, 타깃 잃음.
- 45s 뒤 패치 사라짐. 2인: 클라 패치 표시·재채기 연출 로그.

## 검증 결과 (2026-09-24)
- 0.5m에서 놓기 → 안 쏟아짐, 3m에서 떨어뜨리기 → 쏟아짐(패치 중심 -6,0,-14).
- 냄새: 안 3개 + 밖 1개 추가 → 0.3s 뒤 1개만 남음.
- 추격: 고양이가 쥐를 쫓다 패치 가장자리(z -12.02)에서 재채기 → 2.0s → Return, 타깃 0. 전이: Patrol>Chase>Blunder(Sneeze)>Return.
- 45s 뒤(timeScale 8) 패치 사라짐, ActivePatches 0.
- 2인: 빌드 클라 "후추 연출: 쏟아짐" + "고양이 실패 연출: Sneeze", 예외 0.
- 첫 시도 무효: 패치 밖에 심은 테스트 자국을 고양이가 Track으로 따라가 추격 셋업이 깨짐 — 셋업 순서 문제.
- 미검증: 실제 던지기 입력으로 쏟기(IsRecentlyThrown 경로), 들고 달리다 벽에 박아 쏟기(4 m/s — 웃기면 사양).
