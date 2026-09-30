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
해당 문서의 표와 일치시킨다. (인스펙터에서 실시간 튜닝 → 플레이테스트 속도가 곧 품질)
