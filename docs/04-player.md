# 04. 플레이어 (W2)

## 컴포넌트 구성 (Player 프리팹)

```
Player (Rigidbody, CapsuleCollider, NetworkObject)
 ├ PlayerController        — 이동·점프·웅크리기 (소유 클라에서만 시뮬)
 ├ PlayerCarryController   — 잡기·던지기 (05 문서)
 ├ PlayerInteractor        — E 상호작용 (문·자판기·구출·함정 해제)
 ├ PlayerCondition         — Active/Stunned/Trapped/Downed 상태 (호스트 권한)
 ├ PlayerStamina           — 스태미나 (소유 클라 시뮬, 표시용)
 ├ PlayerNoiseEmitter      — 이동·착지 소음 (06 문서)
 ├ PlayerAnimatorLink      — 상태→애니 파라미터 브릿지
 ├ ClientNetworkTransform + NetworkRigidbody
 └ CameraTarget (빈 오브젝트 — Cinemachine 타깃)
```

## PlayerController — 이동 사양

Rigidbody 기반 (CharacterController 아님 — 물리 상호작용 필수).

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| walkSpeed | 3.8 m/s (2026-09-14 4.5에서 낮춤) |
| sprintSpeed | 6 m/s (7에서) |
| crouchSpeed | 1.9 m/s (2.2에서) |
| acceleration | 40 (지상) / 10 (공중) |
| jumpImpulse | 5.5 |
| coyoteTime | 0.1s |
| jumpBuffer | 0.1s |
| 회전 | 이동 방향으로 즉시 회전 (12f slerp) — 카메라 방향 아님 |

- 카메라: **1인칭** (2026-09-12 결정, `Player/PlayerCameraRig`) — 눈 = 몸 중심 위 0.45m·앞 0.12m, 몸은 시선 yaw를 따라 돈다. 내 몸은 그림자만. near clip 0.05 (손에 든 물건이 눈앞 0.3~0.5m라 기본 0.3이면 잘려서 사라짐).
- **손 위치(HandAnchor)** (2026-09-14): 몸 로컬 (0.75, 0.50, 0.95) × 루트 스케일 0.6 = 눈 기준 **오른쪽 0.45 · 아래 0.15 · 앞 0.45**. 소형 물건은 스프링 조인트라 여기서 약 0.2m 처져 화면 오른쪽 아래에 보인다 (정면 시 GrayBox_S 화면 약 14%, 조준점 안 가림). 이전 위치(0.40, 0.85, 0.90)는 눈높이 정면이라 63% 가림. 손 모델은 아트 단계에서 이 앵커에 붙인다. 대형은 별도 앵커(몸 수직축)라 무관.
- 이동 로직: 카메라 기준 입력 벡터 → `Rigidbody.AddForce`로 목표 속도 추종 (velocity 직접 대입 금지 — 운반 물리와 충돌).
- 접지 판정: SphereCast (반경 0.15, 거리 0.25). 경사 40°까지 보행.
- **등반 없음** (파쿠르 컷) — 대신 점프 높이로 가구 단차를 넘게 레벨 디자인. (P1: 벽 모서리 매달리기)

## 웅크리기 (은신)

- 콜라이더 높이 50% 축소, crouchSpeed 적용.
- 효과: 이동 소음 0 (06 문서), 고양이 시야 감지 거리 50% (07 문서).
- 토글/홀드는 설정에서 선택 (기본 홀드).

## PlayerStamina

| 수치 | 값 |
|---|---|
| max | 100 |
| sprint 소모 | 20/s |
| 무거운 운반(2인 필요 아이템 단독 시도 포함) 소모 | 15/s |
| 회복 | 25/s (미소모 1s 후) |
| 0 도달 | sprint 불가 + 1.5s 헐떡임(소음 발생, 06) |

- 치즈류 전리품을 "먹기"(Interact 길게)로 즉시 50 회복 — 아이템 소멸(가치 포기). 갈등 설계.

## PlayerCondition — 상태이상 (호스트 권한, NetworkVariable)

```csharp
public enum ConditionState { Active, Stunned, Trapped, Downed }
```

| 상태 | 진입 | 효과 | 해제 |
|---|---|---|---|
| Stunned | 감전 전선, 높은 낙하(>6m) | 2s 조작 불가 | 시간 경과 |
| Trapped | 끈끈이 | 이동 불가, 잡기 불가 | 동료가 Interact 1.5s 홀드 |
| Downed | 고양이 포획, 쥐덫 | 조작 불가 + 래그돌 + **CarryableItem화** | 동료가 쥐구멍까지 운반 (09 문서) |

- Downed 시: 들고 있던 아이템 전부 드랍(호스트가 조인트 해제), 본인 시점은 관전 카메라(동료 시점 순환).
- 다운된 몸은 mass 3의 Carryable로 등록 → 1인 운반 가능(느림).

## PlayerInteractor

- 카메라 전방 SphereCast(반경 0.3, 거리 1.2)로 `IInteractable` 탐지 → HUD에 프롬프트 표시.
- 우선순위: 구출(Trapped/Downed 동료) > 함정 해제 > 문/레버 > 자판기.
- 홀드형 상호작용은 진행 게이지를 InteractProgressClientRpc로 전파.

```csharp
public interface IInteractable
{
    string PromptText { get; }          // "구출하기", "문 열기"
    float HoldSeconds { get; }          // 0 = 즉시
    bool CanInteract(ulong clientId);
    void ServerInteract(ulong clientId); // 호스트에서만 호출됨
}
```

## Squeak(찍찍) & Ping

- Squeak: 애니+사운드 전 클라 재생(ClientRpc) + **소음 이벤트 발생** (loudness 30, 06 문서) — 소통에 리스크.
- Ping: 카메라 레이캐스트 지점에 3초 마커(전 클라 표시). 소음 없음. 쿨다운 1s.

## 애니메이션 파라미터 (PlayerAnimatorLink → Animator)

`Speed(float), IsGrounded(bool), IsCrouching(bool), CarryMode(int: 0없음/1한손/2양손/3팀)`,
`Trigger: Jump, Throw, Squeak, Down, Revive`
— 엄지성 애니 8종(GDD)과 1:1. 애니 없을 동안은 캡슐+파라미터 로그로 개발.

## 수용 기준 (W2)

- [ ] MPPM 2인: 상대 이동이 부드럽게 보간, 점프·웅크림 상태 동기화
- [ ] 경사·단차·좁은 틈(웅크려야 통과) 이동 검증용 그레이박스 코스 완주
- [ ] Stunned/Trapped/Downed 전부 호스트 판정으로 재현, 구출 플로우 동작
- [ ] 스태미나 UI 연동(임시 바), 치즈 먹기 회복 동작
