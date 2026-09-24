# 고양이 35 — 가지고 놀기 3단계: 버둥거리기 (2026-09-24, design/cat-ideas/04 "버둥거리기(좌우 연타)로 Bat 위치를 살짝 조종")

## 목표
잡힌 쥐도 **할 일**이 있게(04 리스크: 잡혀 있는 30초가 지루하면 최악). A·D를 번갈아 연타하면 버둥 — 고양이가 툭 칠 때 **내가 보는 쪽으로** 더 멀리 굴러가고(쥐구멍·동료 쪽으로), 고양이는 더 빨리 질린다.

## 설계
- `Player/PlayerStruggle` (Player 프리팹): 소유 클라가 Pinned일 때 Move 입력 x의 부호가 바뀔 때마다(A→D→A…) `StruggleServerRpc`. 호스트는 최소 간격 `catToyStruggleMinInterval` 0.08s로 거르고 횟수를 센다. `RecentlyStruggling` = 1s 안에 버둥.
- 잡힌 쥐 몸은 Pinned여도 카메라 yaw를 따라 돈다(PlayerController) → 서버가 보는 쥐의 forward = 굴러갈 방향.
- `CatBrain.Nudge`: 버둥 중이면 무작위 대신 쥐 forward로, 거리 × `catToyStruggleNudgeMul` 2 (0.4 → 0.8m).
- Bat 끝: 이번 Bat 동안 버둥 횟수 `catToyStrugglePressesPerDrain` 6번마다 관심 -`catToyStruggleDrain` 8 추가.
- 토스트(본인): Pinned 되면 "잡혔다! A·D 번갈아 연타 = 버둥 — 보는 쪽으로 굴러간다".

## 검증 (2인 — 호스트가 잡히는 쪽, 빌드 클라가 동료, 옮기기 확률 0)
- 버둥 없이 Bat 1회: 이동 방향 무작위, 0.4m씩.
- 실제 키 입력(InputSystem 큐로 A/D 번갈아) + 시선 +x: Bat 1회 동안 쥐가 +x로 ~1.6m, 관심 감소 35+8k.
- 클라 예외 0.

## 검증 결과 (2026-09-24, 에디터 호스트가 잡힘 + macOS 빌드 클라 동료, 옮기기 확률 0)
- 버둥 없는 Bat: 0.57m, 방향 무작위(방 가운데 쪽 dot 0.61), 관심 100 → 65.
- 실제 키 입력(InputSystem 큐로 A·D 0.12s 간격 16회) + 시선을 방 가운데로: Bat 한 번에 1.71m, 보는 쪽 dot 0.98, 버둥 15회 인정(첫 키는 번갈이 아님) → 관심 65 → 14 (-35 기본 -16 버둥).
- 클라 예외 0. 본인 토스트 "잡혔다! A·D 번갈아 연타…" 추가.
- 미검증: 원격 클라가 버둥(빌드에 키를 넣을 수 없음 — ServerRpc 경로는 호스트와 같다), 게임패드 스틱 좌우.
