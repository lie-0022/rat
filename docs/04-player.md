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

### 쥐 모델 (2026-09-24)

- 원본: 드라이브 `쥐 게임/개발/모델링 파일/mouse.fbx` → `Art/Models/Rat/mouse.fbx`. 블렌더 FBX, **정적 메시**(뼈대·애니 없음), 텍스처 없이 머티리얼 색 5칸(털 회색·귀코손발꼬리 핑크·배 진분홍·눈 흰색·눈동자·수염 검정).
- `Tools/RatGame/Player/Apply Rat Model`: `Prefabs/Player/RatModel.prefab`(완성 몸 `Cylinder` + 수염만 켬, 작업용 조각 `Cube` 블록 머리·`Cylinder.001` 몸 껍데기는 끔)을 캡슐 높이(1.2m)에 맞추고 발을 캡슐 바닥에 → Player 프리팹의 그레이박스 캡슐 렌더러·귀·코·꼬리를 지우고 자식으로. 얼굴 = +Z. 콜라이더·물리 그대로.
- 1인칭: 자기 모델은 그림자만(PlayerCameraRig.HideOwnBody가 MeshRenderer 전부 처리). 웅크리면 루트 스케일이라 모델도 같이 눌린다.
- 쓰러진 몸(DownedBody, 고양이 69): 같은 도구가 캡슐 렌더러·메시를 **지우고**(끄기만 하면 CarryableItem 주머니 표시가 다시 켠다) 쥐 모델을 자식으로 → 캡슐이 옆으로 누워 있어 모델도 옆으로 누운 쥐. 콜라이더·질량·운반 물리는 그대로. 털 = 주인의 실제 털 색(팔레트·스킨)을 회색 쪽으로 40%.
- **걷는 흔들림 (고양이 71)**: `Player/RatModelWobble` — 뼈대 없이 RatModel 자식만: 보이는 이동 속도로 통통 튀기(6cm)·좌우 뒤뚱(±9°)·앞 기울기(속도당 2.2°, 최대 14°), 멈추면 숨쉬기(±1.5%). 원격 사본도 보간 위치로 똑같이 계산 — 동기화 없음. 텔레포트(한 프레임 2m 넘게)는 걸음으로 안 침.
- 남은 것: 뼈대·진짜 걷기 애니 (모델링 쪽).

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
| teleportGroundWaitSeconds | 5s (텔레포트 지점 바닥 대기 상한) |
| fallRescueY | -10 (이보다 떨어지면 시작 위치로) |

- 카메라: **1인칭** (2026-09-12 결정, `Player/PlayerCameraRig`) — 눈 = 몸 중심 위 0.45m·앞 0.12m, 몸은 시선 yaw를 따라 돈다. 내 몸은 그림자만. near clip 0.05 (손에 든 물건이 눈앞 0.3~0.5m라 기본 0.3이면 잘려서 사라짐).
- **손 위치(HandAnchor)** (2026-09-14): 몸 로컬 (0.75, 0.50, 0.95) × 루트 스케일 0.6 = 눈 기준 **오른쪽 0.45 · 아래 0.15 · 앞 0.45**. 소형 물건은 스프링 조인트라 여기서 약 0.2m 처져 화면 오른쪽 아래에 보인다 (정면 시 GrayBox_S 화면 약 14%, 조준점 안 가림). 이전 위치(0.40, 0.85, 0.90)는 눈높이 정면이라 63% 가림. 손 모델은 아트 단계에서 이 앵커에 붙인다. 대형은 별도 앵커(몸 수직축)라 무관.
- 이동 로직: 카메라 기준 입력 벡터 → `Rigidbody.AddForce`로 목표 속도 추종 (velocity 직접 대입 금지 — 운반 물리와 충돌).
- 접지 판정: SphereCast (반경 0.15, 거리 0.25). 경사 40°까지 보행.
- **텔레포트 바닥 대기·낙하 구조** (2026-09-24, 고양이 59): 스테이지 도착 텔레포트(소유 클라 RPC)가 방 스폰보다 먼저 오면 발밑(3m)에 바닥이 없다 → 바닥이 생길 때까지 제자리 고정(`teleportGroundWaitSeconds` 5s). 안전망으로 y < `fallRescueY`(-10)이면 가까운 PlayerSpawn(없으면 마지막으로 서 있던 곳)으로 옮긴다.
- **등반 없음** (파쿠르 컷) — 대신 점프 높이로 가구 단차를 넘게 레벨 디자인. (P1: 벽 모서리 매달리기)

## 웅크리기 (은신)

- 콜라이더 높이 50% 축소, crouchSpeed 적용.
- 효과: 이동 소음 0 (06 문서), 고양이 시야 감지 거리 50% (07 문서), 냄새 자국 간격 2배(치즈를 들었을 때 덜 남김, 2026-09-24).
- 토글/홀드는 설정에서 선택 (기본 홀드). 구현: 설정 패널 "웅크리기 — 누르고 있기/눌러서 전환" → `SettingsService.Current.CrouchToggle`을 `PlayerController`가 매 프레임 읽음. 전환 모드에서 메뉴가 열려 있는 동안 누른 키는 무시(웅크린 상태는 유지) (2026-09-15, docs/12).

## PlayerStamina

| 수치 | 값 |
|---|---|
| max | 100 |
| sprint 소모 | 20/s |
| 무거운 운반(2인 필요 아이템 단독 시도 포함) 소모 | 15/s |
| 회복 | 25/s (미소모 1s 후) |
| 0 도달 | sprint 불가 + 1.5s 헐떡임(소음 발생, 06) |

- 치즈류 전리품을 "먹기"(Interact 길게)로 즉시 50 회복 — 아이템 소멸(가치 포기). 갈등 설계.
  - 구현 (2026-09-24, 고양이 48, `World/EdibleItem`): 손에 든 Edible 소형(주머니·대형 제외)을 E `eatSeconds` 1s → 호스트가 손에서 빼고 디스폰, 먹은 쥐 클라에 `PlayerStamina.Restore(eatStamina 50)` + `EventBus.CheeseEaten` → 토스트 "냠냠 — 스태미나 +50". HUD 홀드 게이지는 기존 상호작용 게이지 그대로.

## PlayerCondition — 상태이상 (호스트 권한, NetworkVariable)

```csharp
public enum ConditionState { Active, Stunned, Trapped, Downed, Hidden, Pinned }
```

| 상태 | 진입 | 효과 | 해제 |
|---|---|---|---|
| Stunned | 감전 전선, 높은 낙하(>6m) | 2s 조작 불가 | 시간 경과 |
| Trapped | 끈끈이 (`World/GluePad`) | 이동 불가, 잡기 불가 | 동료가 Interact 1.5s 홀드 — 풀려난 쥐는 같은 끈끈이에 4s 안 붙음 |
| Hidden | HideSpot에 E 홀드 0.3s (2026-09-24, `World/HideSpot`) | 이동·잡기 불가, 고양이 시야 판정 제외. 대형을 끌고는 못 들어감 | E 즉시 나오기(소음 35) 또는 고양이가 건드려 발각. 고양이가 입구에 앉아 있으면(BoxSit) 드나들 수 없음 — 갇힘 |
| Pinned | 고양이가 잡고 가지고 노는 중 (동료가 있을 때, 2026-09-24, docs/07 Toy) | 이동·잡기 불가, 찍찍 가능, 든 물건 떨어뜨림 | 고양이가 놓아줌(Release·질림) / 동료가 미끼 / 30s 뒤 Downed |
| Downed | 고양이 포획, 쥐덫 | 조작 불가 + 래그돌 + **CarryableItem화** | 동료가 쥐구멍까지 운반 (09 문서) |

- Downed 시: 들고 있던 아이템 전부 드랍(호스트가 조인트 해제), 본인 시점은 관전 카메라(동료 시점 순환).
- 다운된 몸은 mass 3의 Carryable로 등록 → 1인 운반 가능(느림). 구현은 호스트 소유 대리 몸(docs/05 "다운된 동료 운반", 2026-09-24 고양이 46).

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
- **찍찍 자막 (고양이 158, 오디오 전)**: `SqueakClientRpc` → `EventBus.RatSqueak(owner, pos)` → `ToastWidget` — 다른 쥐 `squeakHearMeters`(30m) 안이면 "(찍찍!) 파랑 쥐 · 왼쪽 26m"(방향·거리), 내 찍찍은 안 뜸, 같은 쥐 1.5초에 한 번(표시만 — 소음은 매번).

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| squeakLoudness | 30 (고양이·소음, docs/06) |
| squeakHearMeters | 30 m (동료 자막, 고양이 158) |

- Ping: 카메라 레이캐스트 지점에 3초 마커(전 클라 표시). 소음 없음. 쿨다운 1s.
- 구현 (2026-09-16, `Player/PlayerPing`): F 또는 휠클릭(패드 R3) → 카메라 정면 레이(트리거·내 몸·든 물건 제외, 안 맞으면 최대 거리 지점) → `PingServerRpc`(서버가 쿨다운만 확인, 여유 0.1s) → `PingClientRpc`(SendTo.Everyone) → 각 클라 로컬 `EventBus.PingReceived` → `UI/PingMarkerWidget`. 메뉴 패널이 열려 있으면 입력 무시.

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| pingCooldownSeconds | 1 s (클라·서버 둘 다) |
| pingMarkerSeconds | 3 s |
| pingMaxDistance | 30 m |


### Sniff(킁킁) — 목적지 냄새 (2026-09-25 고양이 76, 잠정)

- R(패드 십자 아래) → 내 화면에만 3초 동안 **냄새 줄기**: 발밑에서 목적지(식량 창고)로 가는 길의 **다음 문**까지 작은 알갱이가 흘러간다. 목적지방이면 창고까지. 소음 없음·동기화 없음(소유 클라 로컬).
- 길 찾기(`Run/GridRoute`): 클라에도 있는 격자 방(`GridRoom` 위치·`OpenSides` NV)으로 방 그래프를 만들어 BFS — NavMesh는 호스트에만 있어서 안 쓴다. 통로 안이면 양 끝 방 중 목적지에 가까운 쪽 문. 격자 방이 없는 맵(창고)은 창고로 곧장.
- 흐름: `Player/PlayerSniff` → `EventBus.SniffHint(from, to, seconds)` → `UI/SniffTrailView`(규칙 3). 메뉴 패널이 열려 있으면 입력 무시.
- **음식 냄새 (고양이 150, 잠정)**: 킁킁이 성공하면 내 쥐 `sniffFoodMeters` 안의 가치 있는 물건(주머니 속·쓰러진 몸·유인물·상점 물건 제외) 중 가까운 `sniffFoodMax`개 위로 같은 시간 동안 **냄새 김**(알갱이 4개가 차례로 떠오름). `EventBus.SniffFood(spots, seconds)` → `SniffTrailView`. 넓어진 벽 속 방(고양이 141)에서 드문 음식을 찾게. 창고에서도 같이.
- 왜: 벽 속은 방 16~18개 미로라 목적지 불빛이 안 보이는 곳에서 헤매기 쉽다. 다음 문만 알려줘서 길 전체를 주지는 않는다.

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| sniffCooldownSeconds | 10 s |
| sniffTrailSeconds | 3 s |
| sniffFoodMeters | 14 m |
| sniffFoodMax | 8 |

## 애니메이션 파라미터 (PlayerAnimatorLink → Animator)

`Speed(float), IsGrounded(bool), IsCrouching(bool), CarryMode(int: 0없음/1한손/2양손/3팀)`,
`Trigger: Jump, Throw, Squeak, Down, Revive`
— 엄지성 애니 8종(GDD)과 1:1. 애니 없을 동안은 캡슐+파라미터 로그로 개발.

## 수용 기준 (W2)

- [x] MPPM 2인: 상대 이동이 부드럽게 보간, 점프·웅크림 상태 동기화 — (2026-09-25 고양이 137, MPPM 창 대신 에디터 호스트 + 빌드 클라) 클라에서 호스트 쥐를 매 프레임 잼(DevRemoteControl `trace:`): 2초 걷기 217프레임·8.10m(호스트 8.72m — 보간 지연만큼), 프레임 최대 0.077m(평균 0.037m), 튐 0, 멈춘 프레임 4(시작 지연). 웅크림: 클라에서 호스트 키 0.30 ↔ 서면 0.60(`report`). 점프: 클라에서 최고 y 2.27(호스트 2.32), 프레임 최대 0.053m. 오류 0
- [x] 경사·단차·좁은 틈(웅크려야 통과) 이동 검증용 그레이박스 코스 완주 — (2026-09-25 고양이 136) `Tools/RatGame/Test/Create Movement Course` → `Scenes/Test_MovementCourse`(빌드 목록 밖, Play하면 자동 호스트). 곧게 걷기(강제 입력)로: 경사 15°·25°·35°(각 1m) 통과 1.7~2.1s · 단차 0.1m 걸어서 통과 · **0.2m·0.3m는 걸어서 막힘 → 점프로 통과**(기본 점프 정점 1.54m, 업그레이드 3단계 ×1.3) · 웅크림 터널(천장 0.9m) 서서 막힘 / 웅크려 통과 2.8s. 레벨 디자인 메모: **걸어서 넘는 턱은 0.1m까지**, 그 위는 점프(등반 없음 원칙과 맞음)
- [x] Stunned/Trapped/Downed 전부 호스트 판정으로 재현, 구출 플로우 동작 (2026-09-24 고양이 49: 함정 3종으로 — 끈끈이 Trapped → 호스트가 실제 E 1.53s로 클라 구출 / 쥐덫 Downed → 대리 몸 운반 부활(고양이 46) / 전기선 Stunned 2.1s → Active. 2인·클라 예외 0)
- [x] 스태미나 UI 연동(임시 바), 치즈 먹기 회복 동작 (UI `StaminaRing` 기존. 고양이 48: 치즈 조각 들고 실제 E 1.01s → 먹음·스태미나 20 → 95(+50 + 재생)·손 빔·토스트 이벤트)
