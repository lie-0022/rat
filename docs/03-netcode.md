# 03. 넷코드 (W1–2) — 프로젝트 최대 리스크, 최우선 구현

> 목표물: **"4명이 스팀으로 접속해 회색 박스를 같이 들고 옮기는 Sandbox_Net 씬"**.
> 이것이 W2 말까지 안 되면 폴백(탑쌓기 싱글) 논의. 콘텐츠 구현은 이 검증 뒤에만 진행한다.

## 단계 전략

| 단계 | 트랜스포트 | 테스트 방법 |
|---|---|---|
| A (W1) | UnityTransport (127.0.0.1) | Multiplayer Play Mode 에디터 2~4인 |
| B (W2) | Facepunch (Steam Relay) | 실제 스팀 계정 2개, AppID 480 |

트랜스포트는 `NetworkLauncher`에서 런타임 스위치 가능하게 구현 (에디터=Unity, 빌드=Steam 기본, 커맨드라인 `-unitytransport`로 강제).

## Net/NetworkLauncher.cs

```csharp
public class NetworkLauncher : MonoBehaviour
{
    public static NetworkLauncher Instance;
    public async Task<bool> StartHostAsync();          // 로비 생성 + NGO StartHost
    public async Task<bool> JoinAsync(ulong lobbyId);  // 로비 참가 + NGO StartClient
    public void Shutdown();                            // 정리 + Hub로 복귀
    // 내부: 트랜스포트 선택, NetworkManager 설정, 실패 시 에러 이벤트
}
```

- NetworkManager는 Boot 씬 프리팹. Player Prefab 자동 스폰 **끄고** `NetPlayerSpawner`가 수동 스폰
  (허브에서는 허브용, 런에서는 런용 위치에 스폰하기 위해).
- ConnectionApproval 사용: 최대 4명, 게임 진행 중(midgame) 참가는 거부(v1은 로비에서만 합류). 맵 이동 중(`RunSession.DepartPending` — 발판 출발·스테이지 사이 로드 동안 상태가 잠깐 Lobby)도 거부, 사유 "다음 맵으로 이동 중 — 기지로 돌아오면 참가할 수 있어요"(2026-09-25 고양이 142 — 그 틈에 들어오면 맵 로드와 겹쳐 물체 동기화가 10초 넘게 밀려 접속 실패했다). 클라는 끊긴 사유를 개발 로그 "끊긴 이유"로 남긴다.

## Net/SteamLobbyService.cs (Facepunch)

```csharp
public class SteamLobbyService
{
    public event Action<Lobby> LobbyCreated, LobbyEntered;
    public async Task<Lobby?> CreateLobbyAsync(int maxPlayers = 4); // friends-only 기본
    public void OpenInviteOverlay();                                 // SteamFriends.OpenGameInviteOverlay
    // GameLobbyJoinRequested 콜백 → NetworkLauncher.JoinAsync 연결 (친구 초대 수락 플로우)
}
```

- 스팀 초대 수락 → 로비 진입 → 로비 데이터의 호스트 SteamId로 NGO 접속. 이 플로우가 "친구 초대 2클릭"의 실체.
- SteamClient.Init(480)은 GameBootstrap에서. 실패해도 게임은 UnityTransport 모드로 뜨게 (개발 편의).

### 구현 (2026-09-16, 태스크 0-6 — 무료 개발 AppID 480)

- **라이브러리**: `Assets/Plugins/Facepunch.Steamworks` = Facepunch.Steamworks **2.5.2** 공식 릴리스(macOS universal dylib·Win64). `Assets/Plugins/FacepunchTransport` = Unity 커뮤니티 Facepunch 트랜스포트 소스(@0fab638) 복사본. 커뮤니티 패키지를 그대로 설치하지 않은 이유: 동봉된 Steamworks 2.3.2의 macOS 라이브러리가 x86_64 전용이라 Apple Silicon 에디터에서 로드 안 됨. 트랜스포트 수정: `Shutdown()`에서 `SteamClient.Shutdown()` 제거(세션마다 Steam이 꺼져 다음 로비를 못 만듦), asmdef 이름 `Netcode.Transports.Facepunch` (README.md).
- **`Net/SteamLobbyService`** (NetworkLauncher 소유, 싱글톤 아님): `SteamClient.Init(480, asyncCallbacks:false)` + 매 프레임 `RunCallbacks`, 로그인 안 돼 있거나 실패하면 `IsAvailable=false` → UnityTransport 로컬 모드. 호스트 = `CreateLobbyAsync(4)` 친구 전용·참가 가능·데이터 `rat_game=1`. 참가 = `JoinLobbyAsync(id)` → 방장 SteamId를 `FacepunchTransport.targetSteamId`에. 초대 수락·친구 목록 "게임 참가" = `SteamFriends.OnGameLobbyJoinRequested` → `NetworkLauncher.JoinStarted`(메인 메뉴 "OO의 방에 접속 중…") → 참가. 게임이 꺼진 채 수락하면 `+connect_lobby <id>` 인자 → 메인 메뉴가 뜬 뒤 참가. 세션 중 초대 수락은 무시(나간 뒤 다시).
- **트랜스포트 선택** (`NetworkLauncher`): 빌드 = Steam, 에디터 = UnityTransport(메뉴 **Tools/RatGame/Net/Use Steam In Editor**로 켬), `-unitytransport` 인자는 항상 로컬. NetworkManager 프리팹에 UnityTransport + FacepunchTransport 둘 다 두고 시작 시 `NetworkConfig.NetworkTransport`를 고른다. 세션을 나가면(`Shutdown`) 로비에서도 나감.
- **오버레이 없이 초대**: Steam으로 실행하지 않은 빌드·에디터는 Steam 오버레이가 꺼져 있다(`SteamUtils.IsOverlayEnabled=false`, macOS에서 확인). 그래서 `InviteFriend(친구Id)`(로비 초대 메시지)와 `GetOnlineFriends()` 제공 — 지금은 DEV 패널 "친구 초대 (목록)"에서 쓴다(정식 UI는 production/plans/ui-05). 오버레이가 켜져 있으면 `OpenInviteOverlay`.
- **메인 메뉴**: Steam 모드 하단 "Steam · 닉네임", "친구 방 참가" = 친구 목록 오버레이 열기 + "초대를 수락하면 바로 들어가요" 안내(오버레이가 없어도 Steam 앱의 채팅 초대·친구 목록으로 수락 가능).
- **확인한 것 (2026-09-16, 한 대)**: 에디터·macOS 빌드 둘 다 Steam 로그인 인식 → 로비 생성 → Steam 트랜스포트로 호스트 → 기지 입장, 세션 나가기 → 로비 나감·Steam 유지. **2계정·2대 원격 접속은 아직 미검증** (아래 절차).

### 2대 원격 테스트 절차 (0-6 수용 기준)

1. 두 PC 모두 Steam 로그인(서로 친구인 계정 2개 — 한 PC에서 두 계정 동시 로그인은 불가), 같은 개발 빌드 준비. Windows PC면 Unity에 Windows Build Support 모듈 설치 후 Windows 빌드.
2. A: 게임 실행 → 호스트 시작 → 기지. B: 게임 실행해서 메인 메뉴에 둔다 (480은 Spacewar라 게임이 꺼진 상태로 초대를 수락하면 Steam이 Spacewar를 실행하려 함 — **B는 먼저 게임을 켜 둔다**).
3. A: Esc로 커서를 풀고 DEV 패널 "친구 초대 (목록)" → B 이름 클릭 (오버레이가 켜진 환경이면 "친구 초대 (Steam 오버레이)").
4. B: Steam 채팅의 초대 수락 → 메인 메뉴 "A의 방에 접속 중…" → 기지 입장, 팀 2/4 · 토스트 "…가 들어왔어요".
5. 막히면: A의 DEV 패널 로비 번호로 B를 `Rat.app/Contents/MacOS/Rat +connect_lobby <번호>`(Windows는 `Rat.exe +connect_lobby <번호>`)로 실행.

## 플레이어 동기화 (결정 사항 — 02 문서와 일치)

- 플레이어: **소유 클라 권한 이동**. `ClientNetworkTransform` (NGO 샘플 클래스 복사) + 소유 클라에서 Rigidbody 시뮬.
  호스트에서는 원격 플레이어 Rigidbody를 kinematic으로.
- 근거: 반응성이 물리 코미디의 생명. 리컨실리에이션은 스코프 초과.

## 물리 오브젝트(전리품) 동기화 — 이 게임의 기술 코어

- 전리품 Rigidbody는 **호스트에서만 시뮬레이션**. 클라에서는 kinematic + NetworkTransform 보간.
- NGO 2.x `NetworkRigidbody` 사용. Interpolation on, 전송 주기 20~30Hz (NetworkTransform 설정).

### 잡기(Grab)의 네트워크 플로우

```
클라: Grab 입력 → PlayerCarryController가 후보 아이템 탐지
  → GrabRequestServerRpc(itemNetId, gripPointIndex)
호스트: 거리(<0.8m)·아이템 상태(잡힘 슬롯 여유) 검증
  → CarryableItem에 캐리어 등록 → GrabConfirmedClientRpc
전 클라: 잡기 연출(손 애니·아이템 하이라이트)
```

- 잡은 뒤 물리 연결은 **호스트에서만** 만든다 (05 문서의 조인트 방식).
- 클라 소유 플레이어가 움직이면: 플레이어 위치는 클라 권한으로 흐르고, 호스트가 그 위치를 읽어
  조인트 타깃을 갱신 → 아이템이 따라온다. 지연으로 인한 고무줄은 감쇠(스프링) 조인트가 흡수.
- **던지기**: ThrowRequestServerRpc(방향, 차지 0~1) → 호스트가 조인트 해제 + AddForce.

### 레이턴시 허용 기준

- 아이템이 손보다 0.1~0.2초 늦게 따라오는 것은 **버그가 아니라 사양** (낑낑대는 연출로 보임).
- 다만 정산(쥐구멍 진입 판정)은 호스트 기준으로만 — 클라 화면과 1프레임 다르더라도 호스트가 정답.

## NetworkVariable 소유 목록 (전 시스템 공통 — 여기서만 선언)

| 위치 | 변수 | 타입 |
|---|---|---|
| RunManager | Phase, RunTotalValue, StashedValue, ReturnReadyCount/NeededCount, ReturnAt, ResultCarriedValue, ResultEndsAt, Contributions 목록(적립·들고 옴·쓰러짐·구조 — 고양이 186) (docs/09) | enum, int…, double, NetworkList |
| PlayerCondition | State (Active/Stunned/Trapped/Downed) | enum |
| PlayerCarryController | CarriedItemNetId (0=빈손) | ulong |
| CarryableItem | CarrierIds (최대4), Durability | NetworkList<ulong>, float |
| CatBrain | State, TargetClientId (추격 대상, 냄새 추적 중이면 자국 주인 — 고양이 173, HUD "? 냄새") | enum, ulong |
| CatSenses | SuspicionGauge (0~catChaseThreshold — HUD 의심 표시 게이지, docs/07·12) | float |
| CatSenses (고양이 171) | LastCause — 게이지를 마지막으로 올린 자극(없음·봤다·발소리·물건 소리·찍찍·쨍그랑). 호스트만, 바뀔 때만 씀. HUD "?" 밑 이유 | byte |
| PlayerSkin | SkinId(스킨 Id), CustomColor(팔레트 털 색, 알파 0 = 없음) — 둘 다 소유 클라 쓰기 (docs/11) | FixedString32, Color32 |
| StageQuota (벽 속 스테이지, 고양이 63·64·108) | StageNumber, StagesPerRun, Quota, Pantry, Finished (docs/09 새 루프), RunHaul, RunBuys (이번 런 누적 — 엔딩 요약), Modifier (오늘의 집 — 위 막대, 고양이 120) | int ×4, bool, int ×2, byte |
| StageShopCounter (목적지방, 고양이 65) | Wallet, PendingText, Closed | int, FixedString128, bool |
| GridRoom (벽 속 방, 고양이 62·79) | OpenSides — 아래 4비트 = 이어진 면(N·E·S·W), 위 4비트 = 그중 쥐 전용 배관 면. 호스트가 스폰 전에 정함 | byte |
| CatBrain (고양이 140) | Belled — 목에 방울(이번 스테이지 끝까지). 호스트만 씀, 목 방울·지도 주황 점·토스트는 각 클라가 값 변화로 | bool |
| CatBrain (고양이 145) | BodyScale — 몸 배율(벽 속 1.9, 창고 1). 스폰 전 초기값으로만, 클라가 큰 고양이 발걸음 흔들림에 씀 | float |
| DeparturePad (기지) | ReadyCount, NeededCount, Counting, DepartAt, TotalValue, Destination (목적지 인덱스, 고양이 59), BestStage, Endings (새 루프 최고 기록, 고양이 74) | int, int, bool, double, int, int, int, int |

- RPC만 쓰는 것 (NetworkVariable 없음): `PlayerPing` 핑 — 소유 클라 `PingServerRpc(pos)` → 서버 쿨다운 확인 → `PingClientRpc(pos)` 전원 (표시용, 위치 검증 없음, docs/04).
- RPC만: `GridZoneBuilder.StageBriefingClientRpc(stage, stages, quota, flags, modifier)` (modifier = 오늘의 집, 고양이 106) — 벽 속 맵을 다 지은 뒤 한 번, 전원에게 스테이지 안내(표시용, 고양이 81). flags: 1 아기 고양이, 2 문지기, 4 배관 고리. 늦게 들어온 클라는 못 받음(표시일 뿐).

## 씬 동기화

- 호스트: `NetworkManager.SceneManager.LoadScene("Stage_Warehouse01", Single)` → 클라 자동 로드.
- 방 모듈 스폰(10 문서)은 씬 로드 완료 이벤트(OnLoadEventCompleted) 후 호스트가 NetworkObject.Spawn.
  - **실측 (2026-09-24 고양이 59)**: 씬 로드 중(씬 오브젝트의 OnNetworkSpawn)에 스폰한 오브젝트는 로딩 중인 클라에 가지 않는다 — 클라 로그에 `[Deferred OnSpawn] ... NetworkObject was not received within the timeout` 경고, 방이 없어 쥐가 허공 낙하. 그래서 `ZoneBuilder`는 기지에서 출발한 경우(RunSession.DepartPending) 로드 완료 뒤에 짓는다. 씬 직접 플레이는 로드 이벤트가 없어 바로 짓는다.
- 시드는 RunManager NetworkVariable로 전달 → 클라는 스폰 결과를 받기만 함 (직접 생성 안 함).

## 접속 해제 처리 (v1 최소 사양)

- 클라 이탈: 들고 있던 아이템 놓기 + 캐릭터 디스폰. 재접속 불가(로비부터).
- 호스트 이탈: 세션 종료 → 전원 Hub로. (호스트 마이그레이션은 스코프 제외 — README에 알려진 제한으로 명시)
- **씬에 놓인 오브젝트 디스폰 (2026-09-25, 고양이 134)**: 창고 데모처럼 씬에 놓인 전리품을 적립·먹기·깨짐·뇌물·유인 소모 때 `Despawn()`(파괴)하면 NGO가 "Destroying in-scene network objects…" 경고. `Core/NetworkObjectExtensions.DespawnSafe()` — 씬 오브젝트면 `Despawn(false)`, 아니면 파괴. 끄기는 각자 `OnNetworkDespawn`(CarryableItem·CatLure)에서 `SetActive(false)`. 확인: 2인 창고에서 씬 치즈 적립 → 호스트 경고 0·비활성, 클라 "씬 물건 끔".
- **시작 자리 (2026-09-25, 고양이 133)**: `NetPlayerSpawner`가 `clientId % 자리 수`로 골라, 실패한 접속·재접속으로 번호가 밀리면 이미 선 쥐와 겹쳤다(4인에서 client 4 = 호스트 자리 → 서로 밀쳐 튕겨 올라 둘 다 낙하 기절). **다른 쥐에게서 가장 먼 PlayerSpawn**으로 바꿈. 확인: 같은 번호(0·2·3·4)로 4인 접속 → 기절 0, 가장 가까운 두 쥐 1.00m(자리 간격).
- **에디터 호스트에 첫 접속이 실패하는 것 (2026-09-25, 고양이 130)**: UnityTransport 참가는 메뉴가 5초 안에 실패를 알리게 10회×0.5초(`NetworkLauncher.JoinConnect*`). 에디터 플레이 모드가 호스트면 핸드셰이크가 느려 빌드 클라의 **첫 시도가 거의 매번** "Failed to connect" → `-autojoin` 재시도(2~3번째)로 붙고, 실패한 시도가 호스트에 유령 연결로 남기도 한다(위 정리가 처리). **빌드 호스트 ↔ 빌드 클라는 첫 시도에 붙음**(client 1이 첫 번호) — 실제 게임(빌드·Steam 참가)엔 해당 없음. 그래서 코드는 그대로, 에디터 호스트 시험은 재시도를 기다린다.
- **유령 연결 정리 (2026-09-24, 고양이 67)**: 접속 승인 중 끊긴 클라가 플레이어 없이 목록에 남으면 씬 로드 완료 이벤트(OnLoadEventCompleted)가 안 와서 스테이지 출발·맵 생성이 멈췄다(고양이 62 2인 테스트에서 발견). `NetPlayerSpawner`가 게임플레이 씬에서 **플레이어 없이 20s(`_ghostSeconds`) 남은 연결을 끊는다**. 확인: 클라 플레이어를 호스트에서 지워 유령을 만들면 20s에 "유령 연결 정리 … 끊음" → 클라는 메인 메뉴로 정상 복귀 → 호스트 출발 4.1s 만에 StageActive.

## Sandbox_Net 씬 구성 (W1 산출물)

평면 + 경사로 + 회색 박스 5종(mass 0.5/2/5/10/20) + 쥐구멍 트리거 1개.
캡슐 플레이어로 잡기·2인 들기·던지기·정산 로그까지. **아트 없음. 이 씬이 게임의 기술 증명.**

## 수용 기준 (W2 종료)

- [ ] 에디터 MPPM 4인: 이동·점프·잡기·놓기·던지기 동기화, 위치 어긋남 눈에 띄지 않음
- [ ] mass 20 박스: 1인 이동속도 20% 미만, 2인 이상 정상 운반 (05 공식 적용) — 1인 ✅(10kg도 0.02 m/s, 고양이 47 — 오히려 너무 느림, 사용자 판단 대기). 2인은 실제 2인 입력 필요
- [ ] 스팀 빌드 2대(다른 PC/계정): 친구 초대 → 접속 → 위 전부 재현
- [x] 호스트 강제 종료 시 클라가 에러 없이 Hub 복귀 (2026-09-24 고양이 54: 빌드 호스트 `-autohost`를 kill -9 → 빌드 클라가 끊김 감지 → "네트워크 종료 — MainMenu 복귀", 예외 0. 에디터 호스트 정상 종료도 같음. 클라에겐 Hub가 호스트 씬이라 메인 메뉴 복귀)
- **스테이지 로드 대기 (2026-09-25 고양이 121)**: 벽 속 자동 출발(RunManager)·맵 짓기(GridZoneBuilder)는 `Run/SceneLoadGate` — 클라별 `OnLoadComplete`를 모으고 누가 끊길 때마다 "지금 접속한 전원이 로드했나"를 다시 본다(NGO `OnLoadEventCompleted`와 먼저 오는 쪽). NGO 이벤트는 로드 시작 때 있던 클라를 기다려서, 반쯤 붙은 유령·멈춘 클라가 있으면 `LoadSceneTimeOut` 120초까지 멈췄다. 확인: 정상 출발 6.1초, 클라를 멈춘(SIGSTOP) 채 다음 스테이지 로드 → 전송 끊김(30초) 뒤 28.7초에 맵 짓기.
- 참고 (2026-09-25 고양이 110): **클라가 강제 종료(kill -9)되면** 호스트는 UnityTransport 끊김 시간(`DisconnectTimeoutMS` 기본 30초) 뒤에야 이탈로 본다 — 그동안 귀환 모임 인원이 1/2로 묶였다가, 30초 뒤 1/1이 되며 혼자 넘어감(벽 속 스테이지 1, 예외 0). 정상 종료는 바로 이탈. 줄이면 나쁜 연결에서 끊김이 잦아져 기본값 유지(사용자 판단 거리).
- [x] 30분 연속 세션에서 메모리 누수·스폰 누적 없음 (고양이 54: 에디터 호스트 + 빌드 클라 2(자동 배회) 30분 실시간, 집주인 사건 75s마다·접시 깨기·끈·후추·레이저 20s마다·다운 3s 뒤 부활. 1분 표본 30개: 관리 힙 793~898MB 톱니(추세 없음), 할당 1020→1024MB(28분 +4MB), 네트워크 오브젝트 22 고정, 씬 오브젝트 161~173. 호스트·클라 에러 0)
