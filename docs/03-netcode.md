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
- ConnectionApproval 사용: 최대 4명, 게임 진행 중(midgame) 참가는 거부(v1은 로비에서만 합류).

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
| RunManager | Phase, StageNumber, RunTotalValue, StashedValue, ReturnReadyCount/NeededCount, ReturnAt, ResultCarriedValue, ResultEndsAt (docs/09) | enum, int…, double |
| PlayerCondition | State (Active/Stunned/Trapped/Downed) | enum |
| PlayerCarryController | CarriedItemNetId (0=빈손) | ulong |
| CarryableItem | CarrierIds (최대4), Durability | NetworkList<ulong>, float |
| CatBrain | State, TargetClientId | enum, ulong |

## 씬 동기화

- 호스트: `NetworkManager.SceneManager.LoadScene("Run_Kitchen", Single)` → 클라 자동 로드.
- 방 모듈 스폰(10 문서)은 씬 로드 완료 이벤트(OnLoadEventCompleted) 후 호스트가 NetworkObject.Spawn.
- 시드는 RunManager NetworkVariable로 전달 → 클라는 스폰 결과를 받기만 함 (직접 생성 안 함).

## 접속 해제 처리 (v1 최소 사양)

- 클라 이탈: 들고 있던 아이템 놓기 + 캐릭터 디스폰. 재접속 불가(로비부터).
- 호스트 이탈: 세션 종료 → 전원 Hub로. (호스트 마이그레이션은 스코프 제외 — README에 알려진 제한으로 명시)

## Sandbox_Net 씬 구성 (W1 산출물)

평면 + 경사로 + 회색 박스 5종(mass 0.5/2/5/10/20) + 쥐구멍 트리거 1개.
캡슐 플레이어로 잡기·2인 들기·던지기·정산 로그까지. **아트 없음. 이 씬이 게임의 기술 증명.**

## 수용 기준 (W2 종료)

- [ ] 에디터 MPPM 4인: 이동·점프·잡기·놓기·던지기 동기화, 위치 어긋남 눈에 띄지 않음
- [ ] mass 20 박스: 1인 이동속도 20% 미만, 2인 이상 정상 운반 (05 공식 적용)
- [ ] 스팀 빌드 2대(다른 PC/계정): 친구 초대 → 접속 → 위 전부 재현
- [ ] 호스트 강제 종료 시 클라가 에러 없이 Hub 복귀
- [ ] 30분 연속 세션에서 메모리 누수·스폰 누적 없음
