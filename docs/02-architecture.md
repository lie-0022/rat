# 02. 아키텍처 (W1)

## 원칙

1. **호스트 권한(Host-Authoritative)** — 게임 상태·물리·AI·판정은 전부 호스트에서만 실행. 클라이언트는 입력 전송 + 결과 표시.
2. **데이터는 ScriptableObject** — 아이템·구역·장비·밸런스 수치는 코드가 아니라 SO 에셋. 튜닝할 때 코드 수정 금지.
3. **시스템 간 통신은 이벤트** — 직접 참조 대신 정적 이벤트 버스. 순환 참조 금지.
4. **씬 의존 최소화** — 매니저는 코드에서 생성(부트스트랩), 씬에는 콘텐츠만.
5. **한 클래스 한 책임** — PlayerController에 잡기 로직 넣지 않는다. 파일 300줄 넘으면 분리 신호.

## 어셈블리 (asmdef)

```
RatGame.Runtime   ← Scripts/ 전체 (게임 코드 단일 asmdef — 2인 팀 규모에선 쪼개지 않는다)
RatGame.Editor    ← Scripts/Editor/ (에디터 툴)
```

## 네임스페이스 = 폴더 구조

```
Scripts/
  Core/        RatGame.Core        — GameBootstrap, GameStateMachine, EventBus, SaveService
  Net/         RatGame.Net         — NetworkLauncher, SteamLobbyService, NetPlayerSpawner
  Player/      RatGame.Player      — PlayerController, PlayerCarryController, PlayerInteractor,
                                     PlayerCondition, PlayerStamina, PlayerAnimatorLink
  World/       RatGame.World       — CarryableItem, DepositZone, TrapBase+파생, InteractableBase, DoorSocket
  AI/          RatGame.AI          — CatBrain, CatSenses, CatMovement
  Noise/       RatGame.Noise       — NoiseSystem, NoiseEmitter, INoiseListener
  Run/         RatGame.Run         — RunManager, RunSession, ZoneGenerator, LootSpawner
  Meta/        RatGame.Meta        — MetaWallet, HubManager, VendingMachine, ProgressionService
  Data/        RatGame.Data        — 모든 ScriptableObject 정의 (LootItemSO, ZoneDefinitionSO...)
  UI/          RatGame.UI          — HudController, RunBar, LobbyUI, ResultScreen, PingMarker
  Editor/      RatGame.Editor
```

## 이벤트 버스 (Core/EventBus.cs)

정적 클래스 + C# event. 넷코드와 무관한 **로컬** 알림 전용 (네트워크 동기화는 NGO가 담당).

```csharp
public static class EventBus
{
    // Run flow
    public static event Action<int /*zoneIndex*/> ZoneStarted;
    public static event Action<int, bool /*quotaMet*/> ZoneEnded;
    public static event Action<RunResult> RunEnded;
    public static event Action<int /*current*/, int /*quota*/> QuotaChanged;
    // Player
    public static event Action<ulong /*clientId*/> PlayerDowned;
    public static event Action<ulong> PlayerRevived;
    // Loot
    public static event Action<LootItemSO, int /*value*/> LootDeposited;
    public static event Action<LootItemSO> LootBroken;
    // Meta
    public static event Action<int> CheeseCoinChanged;
    public static event Action<string /*achievementId*/> AchievementUnlocked;

    public static void RaiseZoneStarted(int i) => ZoneStarted?.Invoke(i);
    // ... 각 이벤트별 Raise 메서드. 구독 해제는 OnDestroy에서 필수.
}
```

규칙: UI·사운드·도전과제는 **이벤트 구독만**으로 동작해야 한다 (게임플레이 코드가 UI를 직접 호출하지 않는다).

## 게임 상태 머신 (Core/GameStateMachine.cs)

```csharp
public enum GameState { Boot, MainMenu, Lobby /*=Hub씬, 파티 대기*/, InRun, RunResult }
```

- 싱글톤 `GameStateMachine.Instance`. 상태 전이는 호스트만 호출 → NGO NetworkVariable로 복제.
- InRun 내부의 세부 흐름(구역 진행)은 RunManager가 소유 (09 문서).
- 전이 규칙: Boot→MainMenu→Lobby→InRun→RunResult→Lobby. 그 외 전이는 에러 로그.

## 넷코드 권한 모델 요약 (상세: 03)

| 대상 | 권한 | 동기화 방식 |
|---|---|---|
| 플레이어 이동 | Owner(클라) 입력 → 호스트 시뮬 | NGO 2.x 기본: Owner authoritative transform은 쓰지 않는다. ClientNetworkTransform 대신 서버 시뮬 + NetworkTransform |
| 전리품/물리 오브젝트 | 호스트 | NetworkRigidbody + NetworkTransform (interpolate) |
| 고양이 AI | 호스트 전용 실행 | NetworkTransform + NetworkVariable<CatState> |
| 게임 상태·할당량·지갑 | 호스트 | NetworkVariable |
| 잡기/상호작용 요청 | 클라 → ServerRpc → 호스트 검증 | RPC |
| 이펙트·사운드 연출 | 각 클라 로컬 | ClientRpc 또는 NetworkVariable OnValueChanged |

**예외 — 플레이어 이동만 Owner 권한**: 2인 팀 스코프에서 서버 리컨실리에이션 구현은 과하다.
플레이어 캐릭터는 소유 클라가 직접 움직이고(ClientNetworkTransform 패턴), 잡기·정산 등 **판정**만 서버 검증한다.
치팅 방어보다 반응성 우선. 이 결정은 문서 03에서 고정한다.

## 소리 (그레이박스 합성음, 2026-09-30 고양이 242~279)

규칙: 소리는 **각 클라 로컬 구독자** — 판정·동기화를 새로 만들지 않는다(예외 하나: 던지기 휙 `CarryableItem.ThrownClientRpc`, 위치 변화로는 던짐을 못 가려서). 볼륨 = 전체(AudioListener) × 효과음/음악/보이스 설정 줄.

| 부품 | 무엇을 듣고 | 소리 |
|---|---|---|
| `Core/ToneSynth` (정적) | — | 코드로 짧은 음을 만들어 캐시(소리 파일 없음) |
| `Data/GrayboxAudioSO` (`Resources/GrayboxAudio`) | — | 소리 고르기·음량·거리 한 곳. 클립 칸을 채우면 합성음 대신 진짜 소리 |
| `AI/CatVoice` (고양이 프리팹) | State·SleepPhase·BlunderKind·Belled NV, 위치 | 냐?·그르렁·쉭·코골이·하악·가르랑·실패·방울·발소리 |
| `UI/WorldSfx` (+ .House·.Music·.Ui·.Feedback·.Countdown, 스스로 생김) | EventBus(찍찍·파문·집 이벤트·CatCue·던지기·도전과제·도감·먹기), StashedValue·Quota·DepartAt·ReturnAt NV, 고양이 Chase | 쨍·정산·집 소리·추격 음악·UI 클릭·보상·카운트다운 |
| `Player/RatFootsteps`·`RatConditionSfx`·`RatHandsSfx` (쥐 프리팹) | 위치·PlayerCondition·CarriedItemNetId | 발소리·덫/끈끈이/쓰러짐/살아남·잡기 |
| `World/WireShock` | On NV | 지지직 |
| `UI/HiddenOverlayWidget` | 내 Hidden·고양이 거리 | 심장 |
| F4 `DevCheckMenu.Sounds` | — | 모든 소리 바로 듣기(개발용) |

## 싱글톤 규칙

- 허용: GameBootstrap, GameStateMachine, NoiseSystem(정적), SaveService, RunManager(씬 수명)
- 그 외 싱글톤 금지. 참조가 필요하면 이벤트 or 인스펙터 주입.

## 코드 컨벤션

- C# 표준: PascalCase 공개, _camelCase 비공개 필드. `[SerializeField] private` 기본.
- NetworkBehaviour의 RPC 네이밍: `DoThingServerRpc`, `DoThingClientRpc`.
- 밸런스 수치 하드코딩 금지 → `Data/Balance/BalanceConfigSO` 하나에 모은다 (수치는 각 문서 표 참조).
- 주석은 "왜"만. Debug.Log는 `#if UNITY_EDITOR` 또는 커스텀 `Log.Dev()` 래퍼.

## BalanceConfigSO (Data/Balance/)

각 시스템 문서의 수치 표를 필드로 가진 단일 SO. 클로드코드는 새 수치가 생길 때마다 여기 추가하고
해당 문서의 표와 일치시킨다 — 그리고 `Tools/RatGame/Docs/Write Balance Table`로 아래 "수치 전체" 표를 다시 쓴다(EditMode가 어긋남을 잡는다, 고양이 334). (인스펙터에서 실시간 튜닝 → 플레이테스트 속도가 곧 품질)

### 수치 전체 (자동 생성, 고양이 334)

지금 BalanceConfig 값. 바꾼 뒤 메뉴로 다시 쓴다 — 설명·근거는 각 시스템 문서 표에.

<!-- balance:start -->
<!-- 자동 생성 — Tools/RatGame/Docs/Write Balance Table. 손으로 고치지 말 것 -->

#### 런 구성 (docs/00)

| 필드 | 값 | 메모 |
|---|---|---|
| maxPlayers | 4 |  |
| maxZonesPerRun | 5 |  |
| roomModulesPerZone | (3, 4) |  |

#### 기지 출발·귀환 (docs/09·11)

| 필드 | 값 | 메모 |
|---|---|---|
| departCountdownSeconds | 3 | 전원 출발 발판 집합 후 출발까지 (s) |
| returnCountdownSeconds | 3 | 전원 쥐구멍 집합 후 귀환까지 (s) |
| resultScreenSeconds | 8 | 귀환·전멸 결과 화면 표시 시간 (s) |

#### 새 루프 — 스테이지·할당량 (docs/09, 2026-09-24 잠정)

| 필드 | 값 | 메모 |
|---|---|---|
| stagesPerRun | 5 | 이만큼 클리어하면 엔딩 |
| stageQuotaBase | 150 | 1스테이지 식량 할당량 |
| stageQuotaPerStage | 75 | 스테이지마다 더해지는 할당량 |

#### 솔로 보정

| 필드 | 값 | 메모 |
|---|---|---|
| soloCatVisionMultiplier | 0.8 | 시야 -20% |

#### 플레이어 이동 (docs/04)

| 필드 | 값 | 메모 |
|---|---|---|
| walkSpeed | 3.8 | 2026-09-14 4.5 → 3.8 (기본 속도 낮춤, 상점 튼튼한 다리로 회복) |
| sprintSpeed | 6 | 7 → 6 |
| crouchSpeed | 1.9 | 2.2 → 1.9 |
| groundAcceleration | 40 |  |
| airAcceleration | 10 |  |
| jumpImpulse | 5.5 |  |
| coyoteTime | 0.1 |  |
| jumpBuffer | 0.1 |  |
| rotationSlerp | 12 |  |
| teleportGroundWaitSeconds | 5 | 텔레포트 지점에 바닥이 아직 없으면(생성 스테이지 방 스폰 전) 이만큼 제자리 대기 (고양이 59) |
| fallRescueY | -10 | 이보다 떨어지면 가까운 시작 위치로 (맵 밖 낙하 안전망) |

#### 운반 (docs/05)

| 필드 | 값 | 메모 |
|---|---|---|
| grabSpring | 600 |  |
| grabDamper | 40 |  |
| grabMaxForce | 80 | mass 무관 고정 — 무거우면 끌리는 게 의도 |
| grabAngularSpring | 50 |  |
| grabRange | 1.2 | 호스트 검증 관용치 |
| grabBreakDistance | 3 |  |
| heavyThreshold | 6 |  |
| carryMinSpeedMultiplier | 0.15 | 혼자 대형 끌 때 기어가는 최저 속도 (0이면 정지 — 재미 없음) |
| carrySlotStandDistance | 0.45 | 자동 대형: 물건 면에서 쥐 몸 중심까지 (m). 쥐 반경 0.3 + 여유 |
| carrySlotSnapTime | 0.2 | 자동 대형: 자리로 붙는 데 걸리는 시간 (s) |
| carryGripHeight | 0.78 | 대형 잡는 자리 높이(바닥에서, m) — 쥐 몸 앵커 높이와 같게. 윗면에 두면 키 큰 물건(통닭 1.6m)에서 관절이 쥐를 들어 올려 호스트 쥐가 떠서 못 걸었다 (고양이 213) |
| largeCarrySlots | 2 | 대형(8~12kg) 잡는 자리 — docs/05 표 "대형 grip 2~3" |
| extraLargeMass | 16 | 이 무게 이상 대형 = 특대 (docs/05 "특대 16~24") — 등급이 아니라 무게로 (통닭·수박은 등급 Large) |
| extraLargeCarrySlots | 4 | 특대 잡는 자리 — docs/05 "특대 grip 4, 권장 3~4" (고양이 212) |
| sprintBlockLoad | 3 |  |
| jumpBlockLoad | 4.5 |  |
| throwSpeedMin | 2 | m/s, 차지 0 |
| throwSpeedMax | 6 | m/s, 만차지 |
| throwMassDampNumerator | 3 | massDamp = Clamp01(3/mass) |
| throwChargeTime | 1.2 | 홀드 만충 시간 |
| throwHitStagger | 0.5 | 맞은 플레이어 비틀거림 |
| throwAimMinDistance | 1.5 | 1인칭 조준 수렴 거리 하한 (m) — 코앞 벽이면 손에서 옆으로 꺾이지 않게 |
| throwAimMaxDistance | 6 | 상한 (m) — 조준선이 아무것도 안 맞으면 이 거리로 모은다 |

#### 소음 (docs/06)

| 필드 | 값 | 메모 |
|---|---|---|
| maxNoiseRadius | 14 | loudness 100 기준 전파 반경 |
| wallAttenuation | 0.6 | NoiseBlocker 1장 통과 시 ×0.6 (−40%) |
| footstepWalkLoudness | 8 |  |
| footstepRunLoudness | 22 |  |
| footstepWalkInterval | 0.35 |  |
| footstepRunInterval | 0.25 |  |
| pipeEchoMultiplier | 1.6 | 배관 안 달리기 발소리 배율 (쇠관 울림, 고양이 87) |
| landingBaseLoudness | 15 | + 낙하높이 × 5, 1m 이상만 |
| landingPerMeter | 5 |  |
| squeakLoudness | 30 |  |
| squeakHearMeters | 30 | 동료가 찍찍 자막을 보는 거리 (고양이 158 — 오디오 전 자막) |
| impactMaxLoudness | 70 |  |
| breakLoudness | 60 |  |
| alarmingLoudness | 50 |  |
| rippleThreshold | 40 | 이상이면 파문 이펙트 |

#### 트레잇 (docs/05)

| 필드 | 값 | 메모 |
|---|---|---|
| fragileBreakSpeed | 5 | 이 속도(m/s) 초과 충돌만 파손 |
| fragileDamagePerSpeed | 0.15 |  |
| crackedValueMultiplier | 0.5 | Durability<0.5 |
| slipperyInterval | 3 |  |
| slipperyChance | 0.12 |  |

#### 상태이상·구출 (docs/04)

| 필드 | 값 | 메모 |
|---|---|---|
| stunSeconds | 2 |  |
| highFallStunHeight | 6 | 이 높이(m) 초과 낙하 → Stunned |
| rescueHoldSeconds | 1.5 | Trapped 동료 구출 홀드 |

#### 함정 (docs/10, 고양이 49)

| 필드 | 값 | 메모 |
|---|---|---|
| trapMouseLoudness | 80 | 쥐덫 격발 (docs/06) |
| glueGraceSeconds | 4 | 구출된 쥐가 같은 끈끈이에 다시 안 붙는 시간 |
| wireOnSeconds | 1.5 | 전기선 켜짐 |
| wireOffSeconds | 1.5 | 꺼짐 — 이 틈에 지나간다 |
| wireStunSeconds | 2 |  |
| wireZapLoudness | 30 |  |
| trapBaitStunSeconds | 2.5 | 쥐덫 미끼를 서서 집으면 (고양이 129) |
| trapBaitHintMeters | 3 | 이 안에 처음 들어오면 미끼 규칙 안내 (고양이 132) |
| catBellHitMeters | 1 | 던진 방울이 떨어진 자리 이 안 고양이에 달림 (고양이 140) |

#### 큰 고양이 발걸음 (고양이 145)

| 필드 | 값 | 메모 |
|---|---|---|
| catStepShakeRadius | 10 | 이 안의 큰 고양이 걸음만 느낀다 |
| catStepShakeAmplitude | 0.05 | 바로 옆에서 시점이 내려앉는 최대 (m) |
| catStrideMeters | 1.4 | 큰 고양이 걸음 폭 (몸 배율 1.9 기준, 배율에 비례) |
| catStepShakeDecaySeconds | 0.12 |  |

#### 핑 (docs/04)

| 필드 | 값 | 메모 |
|---|---|---|
| pingItemSnapMeters | 0.6 | 핑 지점에서 이 안의 물건이면 이름·가치 표시 (고양이 139) |
| pingCooldownSeconds | 1 | 같은 쥐의 다음 핑까지 (서버도 검사) |
| pingMarkerSeconds | 3 | 마커 표시 시간 |
| pingMaxDistance | 30 | 조준 레이 최대 거리 (m) — 아무것도 안 맞으면 이 거리 지점 |
| sniffCooldownSeconds | 10 | 킁킁 다음까지 (docs/04 Sniff, 고양이 76) |
| sniffTrailSeconds | 3 | 냄새 줄기 보이는 시간 |
| sniffFoodMeters | 14 | 킁킁 — 이 안의 음식에서 냄새 김 (고양이 150) |
| sniffFoodMax | 8 | 가까운 순 최대 개수 |
| sniffFoodTierValues | (30, 80) | 값이 이 둘 미만/사이/이상 → 김 3·4·6알 (고양이 160) |

#### 유인 아이템 (docs/07·08)

| 필드 | 값 | 메모 |
|---|---|---|
| lureYarnSeconds | 8 |  |
| lureYarnRadius | 10 | 낙하지점 기준 유인 범위 |
| lureCatnipSeconds | 15 |  |
| lureCatnipRadius | 3 |  |

#### 스태미나 (docs/04)

| 필드 | 값 | 메모 |
|---|---|---|
| staminaMax | 100 |  |
| staminaSprintDrain | 20 | /s |
| staminaHeavyDrain | 15 | /s — 무거운 운반(sprint 차단 하중) |
| staminaRegen | 25 | /s, 미소모 1s 후 |
| eatSeconds | 1 | 치즈 먹기 E 길게 (docs/04, 고양이 48) |
| eatStamina | 50 | 즉시 회복량 |
| staminaRegenDelay | 1 |  |
| exhaustPantSeconds | 1.5 | 0 도달 시 헐떡임 |

#### 업그레이드 (docs/11 상점) — 레벨당 가산

| 필드 | 값 | 메모 |
|---|---|---|
| baseCarrySlots | 2 | 인벤 기본 칸 (2026-09-12 결정) |
| upgradeMoveSpeedPerLevel | 0.08 | 걷기·달리기 +8%/Lv |
| upgradeStaminaPerLevel | 0.2 | 최대 스태미나 +20%/Lv |
| upgradeThrowPerLevel | 0.15 | 던지기 속도 +15%/Lv |
| upgradeJumpPerLevel | 0.1 | 점프 임펄스 +10%/Lv (높이는 제곱이라 약 +21%/Lv) |
| pantLoudness | 18 |  |

#### 고양이 (docs/07)

| 필드 | 값 | 메모 |
|---|---|---|
| catPatrolSpeed | 2 |  |
| catSuspiciousSpeed | 3 |  |
| catChaseSpeed | 5.5 | sprint 7보다 느림 — 밸런스의 축, 신중히 |
| catReturnSpeed | 2 |  |
| catViewDistance | 8 |  |
| catEyeHeight | 0.5 | 시야 선 시작 높이(발밑 위, m) — 예전 코드에 박혀 있던 값 (고양이 180) |
| catEyeHeightScalesWithBody | 끔 | 켜면 눈높이 = 몸 윗면 × 0.8 (판단 12) |
| catCrouchViewMultiplier | 0.5 |  |
| catDarkViewMultiplier | 0.5 | 어둠 구역(LightZone) 속 쥐 (docs/07) |
| catViewHalfAngle | 35 | 시야각 70° |
| catGazeGainPerSec | 60 |  |
| catGaugeDecayPerSec | 20 |  |
| catHearingGain | 0.8 |  |
| catHearThreshold | 10 |  |
| catSuspicionThreshold | 30 |  |
| catChaseThreshold | 100 |  |
| catDirectSightChaseSeconds | 1 |  |
| catCloseSightDistance | 2 |  |
| catLoseSightSeconds | 3 |  |
| catCaptureRange | 1 |  |
| catCaptureSwingSeconds | 0.4 |  |
| catCaptureRadius | 1.2 |  |
| catGroomSeconds | 3 |  |
| catSuspiciousWanderSeconds | 6 |  |
| catSuspiciousTravelSeconds | 10 | 조사 지점까지 가는 길 상한 (배회 6s는 도착 뒤부터) |
| catSleepSenseMultiplier | 0.3 |  |
| catPatrolWaitRange | (2, 5) |  |
| catDistractedSpeed | 4 |  |

#### 고양이 스팟 (design/cat-design/02) — 머무는 시간 s / 그동안 감각 배율

| 필드 | 값 | 메모 |
|---|---|---|
| catBedSleepSeconds | 60 | 잠자리에서 잠드는 시간 (깨면 순찰) |
| catSpotFoodSeconds | 25 |  |
| catSpotFoodSense | 0.5 | 챱챱 소리에 묻힘 |
| catSpotSunSeconds | 40 |  |
| catSpotSunSense | 0.5 | 반쯤 잠 |
| catSpotGroomSeconds | 20 |  |
| catSpotGroomSense | 0.6 |  |
| catSpotAvoidRecent | 2 | 최근 n개 스팟은 다시 안 뽑음 |

#### 고양이 잠의 단계 (design/cat-ideas/08) — 얕은 잠 ↔ 깊은 잠 파동

| 필드 | 값 | 메모 |
|---|---|---|
| catSleepLightRange | (12, 20) | 얕은 잠 지속 (s) |
| catSleepDeepRange | (10, 18) | 깊은 잠 지속 (s) |
| catSleepForecastSeconds | 3 | 단계 전환 예고 (꼬리 씰룩) |
| catSleepLightSense | 0.5 |  |
| catSleepDeepSense | 0.15 |  |
| catHalfAwakeSeconds | 3 | 한쪽 눈 뜸 — 더 자극 없으면 다시 잔다 |
| catHalfAwakeSense | 0.5 |  |
| catDeepWakeGaugeMul | 2 | 깊은 잠은 의심 임계 ×2여야 반응 |

#### 고양이 추격 (design/cat-design/01-3) — 예측·오버슛·코너 감속

| 필드 | 값 | 메모 |
|---|---|---|
| catChaseLeadSeconds | 1 | 타깃 속도로 이만큼 앞을 노린다 |
| catChaseOvershootMeters | 3 | 시야를 잃으면 마지막 진행 방향으로 이만큼 더 가 본다 |
| catChaseCornerSpeedMul | 0.55 | 방향을 크게 틀 때 속도 배율 (지그재그가 통하게) |

#### 숨을 곳·수색 (design/cat-ideas/14)

| 필드 | 값 | 메모 |
|---|---|---|
| hideExitNoise | 35 | 숨은 곳에서 나올 때 부스럭 — 반경 ≈4.9m (걷기 8·달리기 22 기준, docs/06) |
| catSearchRadius | 6 | 마지막 목격점 주변 수색 반경 |
| catSearchSeconds | 25 | 수색 최대 시간 |
| catSearchMaxSpots | 3 |  |
| catSniffSeconds | 2 | 스팟마다 킁킁 |
| catDisturbChance | 0.3 | 스팟을 건드려 안을 확인할 확률 |
| catPounceWindowSeconds | 0.5 | 발각 뒤 쥐가 튀어나갈 틈 |
| catMultiHideSniffMul | 1.5 | 여럿이 같이 숨으면 킁킁 시간 × 인원 × 이 값 |
| hideEnterSeconds | 0.3 | 들어가기 홀드 |

#### 호기심 앞발 (design/cat-ideas/03)

| 필드 | 값 | 메모 |
|---|---|---|
| catCuriosityMinSpeed | 1.5 | 이 속도 이상 움직이는 풀린 물건에 끌림 |
| catCuriousSpeed | 4 |  |
| catPawCountRange | (3, 5) | 앞발 횟수 (최소, 최대 포함) |
| catPawIntervalSeconds | 0.6 |  |
| catPawImpulse | 1.2 | 앞발 한 번 속도 변화 (m/s, 질량 무관) |
| catPawReach | 0.9 | 수평 거리 이 안이면 친다 |
| catCuriousYawnSeconds | 5 | 다 치고 하품 |
| catCuriousCooldownSeconds | 10 | 같은 물건 다시 끌리기까지 |
| catBoredomSeconds | 30 | 같은 물건 누적 이만큼 놀면 질림 |
| catBoredIgnoreSeconds | 60 | 질린 물건 무시 시간 |
| catCuriousReachTimeout | 8 | 못 닿으면(선반 위 등) 포기 |

#### 기억·앙심 (design/cat-ideas/05)

| 필드 | 값 | 메모 |
|---|---|---|
| catMemoryCellSize | 2 | 장소 기억 격자 (m) |
| catMemorySightPerSec | 1 | 쥐를 보는 동안 그 칸 열 +/s |
| catMemoryNoiseMin | 40 | 이 이상 들린 소음만 기억 (+loudness/40) |
| catMemoryDecayPerSec | 0.1 |  |
| catMemoryPatrolChance | 0.3 | 순찰 목적지 고를 때 기억 칸으로 갈 확률 |
| catMemoryMinHeat | 3 | 이 열 이상인 칸만 찾아감 |
| catMemorySniffSeconds | 1.5 |  |
| catGrudgeEscape | 40 | 추격에서 놓치면 그 쥐 앙심 + |
| catGrudgeSqueak | 15 | 들리는 곳에서 찍찍(도발) + |
| catGrudgeDecayPerSec | 0.2 |  |
| catGrudgeThreshold | 50 | 이상이면 "찍힘" |
| catGrudgeSightMul | 1.5 | 찍힌 쥐 시야 게인 배율 |
| catGrudgeHearingMul | 1.3 | 찍힌 쥐 발소리·찍찍 청각 배율 |
| catGrudgeGiveUpBonus | 2 | 성격 추격 포기 시간 + (찍힌 쥐) |
| noiseAttributionSeconds | 3 | 놓은·던진 물건의 충돌·깨짐을 그 쥐 소리로 보는 시간 |
| catGrudgeBreak | 30 | 쥐 탓으로 깨진 소리 + |
| catGrudgeFooled | 20 | 털실에 속은 뒤 던진 쥐 + |
| catGrudgeBribe | 50 | 치즈 뇌물 - |
| catBribeRadius | 1.5 | 고양이 정면 이 안에 내려놓은 치즈 |
| catBribeRecentSeconds | 10 | 이 안에 쥐가 내려놓은 것만 뇌물 |
| catBribeEatSeconds | 4 |  |

#### 냄새 (design/cat-ideas/06)

| 필드 | 값 | 메모 |
|---|---|---|
| scentIntervalMeters | 1.5 | 이만큼 걸을 때마다 자국 1개 |
| scentEdible | 60 | 치즈류를 들었을 때 자국 강도 |
| scentGrudge | 20 | 찍힌 쥐는 빈손이어도 약한 자국 |
| scentDecayPerSec | 3 | 60 → 20s |
| scentMaxMarks | 32 |  |
| scentCrouchIntervalMul | 2 | 웅크리면 간격 × |
| catScentDetectRadius | 3 | 순찰 중 이 반경 자국을 맡음 |
| catScentMinStrength | 8 |  |
| catTrackSpeed | 3 |  |
| catTrackSniffSeconds | 0.5 | 자국마다 킁킁 |
| puddleRadius | 2.5 | 엎은 물그릇 웅덩이 (design/cat-ideas/06 2단계) |
| puddleSeconds | 60 |  |

#### 선반·점프 (design/cat-ideas/09·01·11, 고양이 39)

| 필드 | 값 | 메모 |
|---|---|---|
| catPerchSeconds | 25 | 선반 위에서 내려다보는 시간 |
| catPerchViewDistance | 12 | 선반 위 시야 거리 (평소 8) |
| catJumpSeconds | 0.45 |  |
| catJumpFailChance | 0.15 | 올라가는 점프 실패 |
| catJumpFailStunSeconds | 1.5 |  |

#### 문 (design/cat-ideas/09, 고양이 40)

| 필드 | 값 | 메모 |
|---|---|---|
| doorPushSeconds | 4 | 쫓던 고양이가 닫힌 문을 밀어 여는 시간 |
| doorPushRange | 1.4 | 문 중심에서 이 안이면 미는 중 |
| doorCloseNoise | 25 | 쥐가 문 닫는 소리 |

#### 귀 (design/cat-ideas/13, 고양이 42)

| 필드 | 값 | 메모 |
|---|---|---|
| catEarHearMul | 0.5 | 청각 임계의 이 배 이상 크기면 귀만 쫑긋 |
| catEarRadiusMul | 1.5 | 소리 전파 반경의 이 배까지 귀는 듣는다 (게이지는 반경 안만) |
| catEarHoldSeconds | 2 | 소리 쪽을 이만큼 들음 |

#### 레이저 포인터 (design/cat-ideas/13, 고양이 41)

| 필드 | 값 | 메모 |
|---|---|---|
| laserBatterySeconds | 20 | 든 동안만 닳는다 |
| laserAimRange | 25 |  |
| laserAttractRadius | 12 | 점이 이 안이고 보이면 고양이가 쫓는다 |
| laserLingerSeconds | 5 | 점이 꺼져도 이만큼 두리번 |
| catLaserChaseSpeed | 4.5 | 점을 쫓는 속도 |

#### 흔들리는 끈 (design/cat-ideas/13, 고양이 38)

| 필드 | 값 | 메모 |
|---|---|---|
| stringSwingSeconds | 30 |  |
| stringAttractRadius | 8 | 이 안의 한가한 고양이가 보면 온다 |
| stringDistractSeconds | 10 | 앞발질 시간 |
| stringCatCooldown | 40 | 같은 고양이는 이만큼 다시 안 속음 |
| stringRetriggerRadius | 1.5 | 쥐가 이만큼 옆을 지나가면 다시 흔들림 |

#### 후추 (design/cat-ideas/06, 고양이 33)

| 필드 | 값 | 메모 |
|---|---|---|
| pepperSpillImpactSpeed | 4 | 던지지 않고 떨어뜨려도 이 속도 이상이면 쏟아짐 |
| pepperRadius | 2 |  |
| pepperSeconds | 45 |  |
| pepperSneezeSeconds | 2 | 고양이 재채기 정지 |
| pepperSneezeCooldown | 8 | 같은 고양이 재채기 간격 |
| wetSeconds | 20 | 웅덩이를 지난 쥐가 젖어 있는 시간 |
| scentWet | 40 | 젖은 발자국 강도 |
| waterBowlSpillNoise | 30 |  |
| bedCoverRadius | 1.5 | 고양이 잠자리 이 안에 들어간 쥐는 |
| bedCoverSeconds | 30 | 이만큼 냄새 자국 없음 (고양이 냄새로 덮임) |

#### 가지고 놀기 (design/cat-ideas/04)

| 필드 | 값 | 메모 |
|---|---|---|
| catToySeconds | 30 | 이 안에 못 벗어나면 진짜 다운 |
| catToyInterest | 100 |  |
| catToyLoopDrain | 35 | 툭툭 한 번 끝날 때마다 관심 - |
| catToyDistractDrain | 40 | 즉시 조사 소음(깨짐·찍찍·함정) 관심 - |
| catToyBatSeconds | 2 |  |
| catToyNudge | 0.4 | 앞발로 칠 때 잡힌 쥐가 밀리는 거리 (m) |
| catToyReleaseSeconds | 3 | 놓아주는 창 |
| catToyEscapeDistance | 2 | 놓아준 자리에서 이만큼 벗어나면 도주 → 추격 |
| catToyYawnSeconds | 2 |  |
| catToyResumeWindow | 10 | 다시 잡으면 관심·남은 시간 이어서 |
| catToyBoredIgnoreSeconds | 6 | 질려서 놓아준 쥐는 하품 뒤 이만큼 못 본 척 |
| catToyCarryChance | 0.6 | 물고 옮기기 (고양이 34) — 놀이 한 판에 한 번, 첫 툭툭 뒤 |
| catToyCarrySeconds | 6 |  |
| catToyCarryMinDistance | 4 | 목적지(침대·햇볕) 거리 범위 |
| catToyCarryMaxDistance | 12 |  |
| catToyCarrySpeedMul | 1.2 | 순찰 속도 배율 — 자랑스럽게 종종 |
| catToyCarryDropChance | 0.1 | 초당 — 물고 가다 떨어뜨림 |
| catToyStruggleNudgeMul | 2 | 버둥 (고양이 35) — 툭 칠 때 보는 쪽으로 이 배수만큼 |
| catToyStrugglePressesPerDrain | 6 | 버둥 이만큼마다 |
| catToyStruggleDrain | 8 | 관심 - |
| catToyStruggleMinInterval | 0.08 | 버둥 최소 간격(초) — 매크로 연타 거름 |

#### 댕청한 실패 (design/cat-ideas/11)

| 필드 | 값 | 메모 |
|---|---|---|
| catSlipMinSpeed | 4 | 이 속도 이상으로 달릴 때만 미끄러진다 |
| catSlipDetectRadius | 0.7 | 발밑 Slippery 물건 판정 |
| catSlipDistance | 3 |  |
| catSlipSeconds | 0.6 |  |
| catSlipStunSeconds | 2 | 미끄러지다 벽에 박으면 |
| catSlipRecoverSeconds | 0.8 | 안 박으면 추스르기 |
| catSlipCooldown | 4 |  |
| catStartleRadius | 2.5 | 놀다가 이 안에서 깨짐·찍찍·함정 → 펄쩍 |
| catStartleSeconds | 1.5 |  |
| catWobbleSeconds | 10 | 캣닢 뒤 비틀거림 |
| catWobbleSpeedMul | 0.5 |  |
| catHairballChance | 0.2 | 그루밍 끝나면 헤어볼 확률 |
| catHairballSeconds | 3 | 웩웩 |

#### 루틴 예고·화장실·물 (design/cat-ideas/02)

| 필드 | 값 | 메모 |
|---|---|---|
| catLitterSeconds | 12 |  |
| catLitterSense | 0.3 |  |
| catZoomiesSeconds | 10 | 화장실 뒤 우다다 |
| catZoomiesSpeed | 6.5 | 추격(5.5)보다 빠름 — 목적은 없다 |
| catZoomiesRadius | 8 |  |
| catZoomiesStep | 1.2 | 이 간격마다 새 방향 |
| catWaterSeconds | 8 |  |
| catCueHearRange | 18 | 루틴 예고가 들리는 거리 |

#### 매복·상자 (design/cat-ideas/09)

| 필드 | 값 | 메모 |
|---|---|---|
| catAmbushSeconds | 60 |  |
| catAmbushPounceRange | 2 | 이 안 쥐는 추격 없이 덮친다 |
| catAmbushCueInterval | 10 | "츄릅" 자막 간격 |
| catPounceSwingSeconds | 0.2 | 덮치기 스윙 (보통 0.4) |
| catBoxSeconds | (15, 40) |  |
| catBoxViewDistance | 3 | 상자 안 — 정면 좁게 |
| catBoxViewHalfAngle | 45 |  |
| catBoxBlockRadius | 2 | 이 안 숨을 곳 입구를 막는다 |

#### 경계도 디렉터 (design/cat-ideas/12) — 고양이 감각 수치는 안 건드린다

| 필드 | 값 | 메모 |
|---|---|---|
| directorTickSeconds | 10 |  |
| tensionSuspicious | 10 |  |
| tensionChase | 25 |  |
| tensionToy | 20 |  |
| tensionDown | 40 |  |
| tensionNoise | 5 |  |
| tensionNoiseMin | 40 |  |
| tensionDecayPerSec | 1 |  |
| buildupThreshold | 25 | 긴장이 이 아래이고 |
| buildupQuietSeconds | 60 | 마지막 위기(추격·다운) 뒤 이만큼 → Build-up |
| reliefThreshold | 70 |  |
| reliefSecondsRange | (20, 40) |  |
| buildupSleepMul | 0.7 | Build-up·Finale: 잠 짧게 |
| buildupDwellMul | 0.6 | Look 머무름 짧게 (촘촘한 순찰) |
| buildupMemoryBonus | 0.3 | 기억 칸 방문 확률 + |
| reliefRoutineWeightMul | 4 | Relief: Groom·Sun·Bed 스팟 가중치 × |
| reliefLookWeightMul | 0.3 |  |
| reliefDwellMul | 1.5 | 루틴 머무름 × |
| finaleHoleBias | 0.6 | Finale: 순찰 목적지를 쥐구멍 근처로 |
| finaleHoleRadius | 4 |  |

#### 집주인 이벤트 (design/cat-ideas/10)

| 필드 | 값 | 메모 |
|---|---|---|
| houseEventsPerStage | (2, 4) |  |
| houseEventFirstDelay | (90, 150) |  |
| houseEventGap | (180, 300) |  |
| houseEventWarnSeconds | 3 | 예고음 → 본 사건 |
| catCallAwaySeconds | (20, 40) |  |
| catFeedingSeconds | 25 |  |
| catAwayWalkSpeed | 3 |  |
| reliefCallAwayMinGap | 90 | 디렉터 Relief면 마지막 사건 뒤 이만큼 지났을 때 부르기를 앞당김 |
| doorbellHoldSeconds | 1 | 초인종 E 홀드 (런당 1회) |
| attentionIndicatorRange | 15 | 이 안에서 노는 고양이면 HUD "♪" |
| vacuumSeconds | 30 | 로봇청소기 (집주인 이벤트) |
| vacuumSpeed | 1.5 |  |
| vacuumMaskLoudness | 40 | 이 미만 소리는 청소기 소리에 묻힘 |
| vacuumCatSense | 0.6 | 피신한 고양이 감각 |
| tvSeconds | 90 | TV (집주인 이벤트) |
| tvMaskLoudness | 25 | TV 소리에 묻히는 loudness |
| tvWatchDistance | 2.5 | 고양이가 TV 앞 이만큼에 앉는다 |
| lightOnSeconds | 30 | 불 켜짐 (집주인 이벤트) — 어둠 해제 시간 |
| lightOnGreetSeconds | 10 | 고양이가 집주인 다리에 부비는 시간 |
| lightOnGreetSense | 0.5 | 부비는 동안 감각 |
| windowSeconds | 20 | 창문 바람 (집주인 이벤트, 고양이 37) |
| windowGustSpeed | 3 | 돌풍 한 번에 가벼운 물건에 주는 속도 (m/s) — 꾸준한 힘은 바닥 마찰(≈6 m/s²)을 못 이긴다 |
| windowGustInterval | 2 | 돌풍 간격 (s) |
| windowMaxItemMass | 1 | 이 질량 이하만 바람에 굴러간다 |
| windowMaskLoudness | 20 | 바람 소리에 묻히는 loudness |

#### 두 마리 — 앙숙 (design/cat-ideas/07)

| 필드 | 값 | 메모 |
|---|---|---|
| catFightDistance | 4 | 이 안에서 서로 보이면 |
| catFightChance | 0.7 |  |
| catFightSeconds | 15 |  |
| catFightCooldown | 30 | 싸운 뒤 재발 금지 |
| catFightDeclineCooldown | 10 | 서로 무시하고 지나간 뒤 재판정 금지 |
| catFightViewMul | 0.2 | 싸우는 동안 시야 배율 (서로에게 꽂힘) |
| catBuddyChance | 0.5 | 2마리일 때 짝꿍일 확률 (아니면 앙숙) |
| catFlankRange | 20 | 짝꿍이 추격하면 이 안의 고양이가 협공 |
| catFlankLeadSeconds | 2 | 쥐의 이만큼 뒤 예상 위치로 |
| catFlankSeconds | 8 |  |
| catFlankSpeed | 4 |  |
| catBuddySleepMul | 1.5 | 짝꿍과 같이 자면 수면 × |
| catKittenCallCooldown | 10 | 아기 냐앙 → 엄마 호출 재사용 대기 |
| catGuardRadius | 8 | 문지기가 순찰하는 초소 둘레 (m, 고양이 80) |
| catGuardPostWeight | 4 | 초소 관찰점 가중 배율 |
| houseNewTrapsCount | 2 | 집주인 덫 놓기 — 한 번에 새 함정 수 (고양이 89) |
| houseNewTrapsAvoid | 5 | 쥐에서 이만큼(m) 넘게 떨어진 자리에만 |
| flushSeconds | 6 | 배관 물 내려가는 시간 (고양이 97) |
| flushMaskLoudness | 30 | 물소리 — 이 미만 소리는 묻힘 |

#### 관심 점수 (design/cat-ideas/13, 고양이 261) — 유인 중엔 이보다 낮은 자극을 무시

| 필드 | 값 | 메모 |
|---|---|---|
| attentionScores | 40 · 50 · 60 · 80 · 90 | AttentionKind 순서: 먹이 떨굼·끈·털실·레이저·캣닢 |
| boredomWindowSeconds | 60 | 같은 종류에 이 안에서 |
| boredomCount | 3 | 이만큼 반응했으면 |
| boredomMultiplier | 0.5 | 다음 반응 시간 × |
<!-- balance:end -->
