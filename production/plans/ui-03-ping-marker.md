# 계획 3 — 핑 마커 (입력 → RPC → 월드 마커)

## 목표
docs/04 "Ping: 카메라 레이캐스트 지점에 3초 마커(전 클라 표시). 소음 없음. 쿨다운 1s." + docs/12 `PingMarker — 월드 스페이스 3s 마커 (벽 뒤 투시 아이콘)`. 입력 액션만 있고 코드가 전혀 없으므로 **입력·RPC·마커 UI를 한 태스크로**.

## 사양 근거
- docs/04 Squeak & Ping. docs/01 입력 표: Ping = 마우스 휠클릭 / 패드 R3, Button.
- docs/03 권한: 판정은 호스트. 핑은 판정이 아니라 "전 클라 표시"이므로 **소유 클라 → 서버 RPC → 전 클라 ClientRpc** 중계(호스트가 위치를 검증할 필요는 없지만 쿨다운은 서버에서도 막아 스팸 방지).
- docs/02: EventBus는 로컬 알림 전용. 마커 표시는 ClientRpc로 받은 뒤 **로컬 EventBus `PingReceived(clientId, worldPos)`** 발행 → UI가 구독 (규칙 3 준수: 시스템이 UI를 직접 부르지 않음).
- docs/06: 핑은 소음 없음 (Squeak와 다름 — 이번 태스크는 Squeak 제외).

## 현재 코드
- `Settings/RatInput.inputactions`에 `Ping` 액션 존재(바인딩 확인 필요: 휠클릭 `<Mouse>/middleButton`, 패드 R3).
- `Player/` 에 Ping 처리 없음. `PlayerCameraRig`가 카메라 소유, `Camera.main` 사용.
- HUD는 Screen Space Overlay 캔버스 — 월드 마커는 **월드 좌표 → `Camera.WorldToScreenPoint`로 매 프레임 화면 위치 갱신**하는 오버레이 방식 (World Space 캔버스 안 씀: 벽 뒤 투시가 목적이라 오버레이가 맞음).

## 설계
### 입력·네트워크 (Player)
- 신규 `Player/PlayerPing.cs` (NetworkBehaviour, Player 프리팹에 추가):
  - 소유 클라 `Update`: `Ping` 액션 `WasPressedThisFrame` && `!InputFocus.IsUiOpen` && 쿨다운 지남 → 카메라 정면 Raycast(최대 `PingMaxDistance`, 트리거 제외, 자기 콜라이더 제외). 맞으면 히트 지점, 안 맞으면 최대 거리 지점. → `PingServerRpc(Vector3 pos)`.
  - 서버: 클라별 마지막 핑 시각 검사(쿨다운, 관용 0.1s) → `PingClientRpc(ulong byClientId, Vector3 pos)` 전원.
  - 전 클라 `PingClientRpc`: `EventBus.RaisePingReceived(byClientId, pos)`.
  - RPC 스타일은 프로젝트 최신 방식(`[Rpc(SendTo.Server, InvokePermission = Everyone)]` + `RpcParams`) — VendingMachine 참고. ClientRpc는 `[Rpc(SendTo.Everyone)]`.
- `EventBus`: `event Action<ulong, Vector3> PingReceived` + `RaisePingReceived`.
- BalanceConfig 신규: `_pingCooldownSeconds 1`, `_pingMarkerSeconds 3`, `_pingMaxDistance 30` → docs/04 표에 추가.

### 마커 UI
- 신규 `UI/PingMarkerWidget.cs` (Hud `PingArea`, 항상 켜짐): `PingReceived` 구독(OnEnable/OnDisable). 마커 풀 최대 4개(플레이어 수) — 같은 사람이 다시 찍으면 자기 마커 이동(교체), 남의 것은 유지.
- 마커 모양: 팀색(`PlayerVisual.ColorFor(clientId)`) 다이아몬드/원 Image + 아래 작은 라벨 "회색 쥐" + 거리 "12m"(Small). 벽 뒤여도 그대로 그림(오버레이라 자동 투시). 화면 밖이면 **가장자리에 클램프**하고 화살표 없이 반투명(0.5) — 방향만 알려줌.
- 수명: 3s, 마지막 0.5s 페이드. 크기: 거리와 무관 고정 28px (투시 아이콘은 크기로 거리를 표현하지 않음 — 거리 숫자가 있음).
- 자기 핑도 표시(내가 어디 찍었는지 확인).

### 파일
- `Player/PlayerPing.cs`(신규), Player.prefab에 컴포넌트 추가(execute_code).
- `Core/EventBus.cs` 이벤트 추가.
- `Data/BalanceConfigSO.cs` 수치 3개 + 에셋 값.
- `UI/PingMarkerWidget.cs`(신규), Hud 프리팹 `PingArea`(sortingOrder는 Hud 기본 — RunBar 10 아래, 결과 화면 아래).
- docs/03 표에 RPC 추가 메모(NetworkVariable 아님 — "RPC 목록"이 없으므로 03 접속·RPC 절 끝에 한 줄), docs/04 수치 표, docs/12 구현 현황.

### 하지 않는 것
- Squeak(소음 있는 소통 — 별도), 핑 종류 구분(위험/여기로), 핑 사운드(오디오 단계), 패드 바인딩 검증(키·마우스만 테스트).

## 구현 순서
1. BalanceConfig 수치 + docs/04 표.
2. `PlayerPing` + EventBus + 프리팹 컴포넌트.
3. `PingMarkerWidget` + Hud 영역.
4. 검증(2인 필수) → docs 갱신.

## 에디터 체크리스트
- `RatInput.inputactions`의 Ping 바인딩이 `<Mouse>/middleButton`인지 확인(아니면 인스펙터에서 추가). 나머지는 execute_code.

## 테스트
- 에디터 호스트 1인: 휠클릭 주입(`MouseState.WithButton(MouseButton.Middle)`) → 조준 지점에 마커·라벨·거리 → 3s 뒤 소멸. 1s 안에 두 번 → 한 번만. 벽 뒤 지점을 찍고 뒤돌아 → 화면 가장자리 클램프·반투명. UI 열린 채(상점) 휠클릭 → 무시.
- 2인(에디터 + 빌드 `-autojoin`): 클라가 찍은 핑이 호스트 화면에 파랑/초록 쥐 색으로 뜸(빌드 쪽 입력은 `DevRemoteControl` 경유 가능하면 사용, 아니면 호스트가 찍고 클라 로그로 수신 확인). 호스트 핑도 클라에 도착 — 클라 로그 `PingReceived`.
- 스팸: 서버 쿨다운 — 클라 쿨다운을 0으로 바꾼 채 연타해도 서버가 1s에 1개만 중계(로그 카운트).
