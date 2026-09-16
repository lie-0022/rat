# 계획 5 — 기지 로비 UI (스팀 초대 버튼·방 공개 설정)

## 목표
docs/12 LobbyUI: 좌상단 인원/4(**완료 — TeamStatusWidget**), **스팀 초대 버튼(OpenInviteOverlay)**, **방 공개 설정(친구만/초대만)**. 메인 메뉴 "친구 방 참가"도 로컬 127.0.0.1에서 **스팀 로비 참가**로 바뀐다.

## 선행 조건 — 태스크 0-6 (Facepunch)

> **2026-09-16 0-6 코드 완료** (docs/03 구현 절). 이미 된 것: `SteamLobbyService`(`IsAvailable`·`CreateLobbyAsync`·`JoinLobbyAsync`·`OpenInviteOverlay`·**`InviteFriend`·`GetOnlineFriends`·`IsOverlayEnabled`**), 메인 메뉴 Steam 문구·친구 목록 열기·초대 수락 접속 표시, 팀 상태 안내 문구, DEV 패널 초대 목록. 남은 것: 아래 설계의 일시정지 창 초대 버튼(**오버레이가 꺼진 환경이 기본이므로 게임 안 친구 목록 UI로**), Tab 초대, 방 공개 설정(`SetVisibility` 래퍼 + `SessionInfo` NV). 아래 "0-6이 제공해야 할 API" 중 `SetVisibility`만 아직 없음.
- `Packages/manifest.json`에 Facepunch.Steamworks 없음, `Net/SteamLobbyService` 없음. **이 계획은 0-6이 끝나야 시작**한다. 0-6 자체(트랜스포트·로비 생성·초대 수락 플로우)는 docs/03 `SteamLobbyService` 절이 사양이며 이 계획 범위가 아니다.
- 0-6이 제공해야 UI가 붙는 API (docs/03 기준):
  - `SteamLobbyService.CreateLobbyAsync(maxPlayers)` — 호스트 시작 시 호출(NetworkLauncher 안).
  - `SteamLobbyService.OpenInviteOverlay()`.
  - `LobbyCreated/LobbyEntered` 이벤트, 현재 `Lobby?` 접근자.
  - 로비 공개 범위 설정: `Lobby.SetFriendsOnly()` / `SetInvisible()`(초대만) — 0-6에 **`SetVisibility(LobbyVisibility)` 래퍼**를 넣어 달라고 요구(UI가 Facepunch 타입을 직접 안 만지게).
  - Steam 미초기화(UnityTransport 모드) 여부: `SteamLobbyService.IsAvailable`.

## 사양 근거
- docs/11 Hub = 플레이 가능한 로비, 친구가 스팀 초대로 합류.
- docs/03: 로비 friends-only 기본, 초대 수락 → 로비 진입 → 호스트 SteamId로 NGO 접속.
- docs/12 LobbyUI, 와이어프레임 결정(로비 인원/초대 좌상단, 기지).

## 현재 코드
- `UI/TeamStatusWidget`: 기지에서 "기지 · n/4명" + 안내 문구 "친구 초대는 Steam 연결 후"(hint). 스테이지에선 "팀 n/4".
- `UI/MainMenuController`: 참가 = `NetworkLauncher.JoinAsync()` 로컬. 상태 문구 "로컬 모드 (Steam 연결 전)".
- HUD는 커서가 잠긴 상태 — **버튼을 클릭할 수 없다.** 기지 HUD에 버튼을 두려면 커서 해제가 필요.

## 설계
### 초대 — 버튼 대신 키 + 일시정지 메뉴
- 커서 잠금 HUD에 클릭 버튼은 맞지 않음. 두 경로:
  1. **팀 상태 헤더에 "[Tab] 친구 초대"** 안내 → Tab(입력 액션 `Invite` 신규, 기지·호스트일 때만) → `OpenInviteOverlay()` (Steam 오버레이가 커서를 가져감). 클라에게는 안내 없음(초대는 호스트만? — Steam은 로비 멤버 누구나 초대 가능. friends-only면 멤버 초대 허용. **결정: 멤버 전원 허용**, docs/11 "친구가 스팀 초대로 합류"와 충돌 없음).
  2. **일시정지 창(Esc)에 "친구 초대" 버튼** 추가(기지·세션 중·Steam 가능할 때만 표시) — 클릭 경로. `PauseMenu`에 버튼 하나 추가, `SteamLobbyService.IsAvailable && 씬==Hub`일 때만 활성.
- Steam 미가용(UnityTransport 모드): 헤더 안내를 "로컬 모드 — 같은 PC에서만 참가" 로, 일시정지 버튼 숨김.

### 방 공개 설정 — 호스트만, 설정 패널이 아니라 일시정지 창
- 일시정지 창 기지 구역에 **"방 공개: 친구만 / 초대만"** 스테퍼(`SettingsStepper` 재사용, 호스트만 조작 가능, 클라는 읽기 표시). 값은 로비 데이터이므로 SettingsService(기기 설정)에 저장하지 않고 **세션마다 기본 "친구만"**. 바꾸면 `SteamLobbyService.SetVisibility`.
- 팀 상태 헤더 두 번째 줄에 현재 값 표시 "친구만 공개" (클라도 보이게 — 로비 데이터를 0-6이 `LobbyVisibilityChanged` 이벤트로 주거나, NetworkVariable로 복제. **결정: `DeparturePad`가 아니라 신규 `Net/SessionInfo` NetworkBehaviour(NetworkManager 프리팹)에 `NetworkVariable<byte> LobbyVisibility`** — docs/03 표에 추가). 로비 데이터를 직접 읽게 하면 UI가 Facepunch를 알게 되므로 NV 경유.

### 메인 메뉴 연결 (0-6 결과 반영)
- "친구 방 참가" → Steam 가용 시 **친구 목록 오버레이**(`SteamFriends.OpenOverlay("friends")`) 안내 또는 "초대를 수락하면 자동으로 들어가요" 문구 + 대기 상태. 초대 수락 콜백(`GameLobbyJoinRequested`)이 오면 NetworkLauncher가 접속 → 메뉴 상태 문구 "OO의 방에 접속 중…". 로컬 모드면 기존 동작 유지.
- 하단 "로컬 모드 (Steam 연결 전)" → Steam 가용 시 "Steam · 닉네임".

### 파일
- `UI/TeamStatusWidget`: 헤더 안내 분기(Steam 가용/로컬), 공개 상태 줄.
- `UI/PauseMenu` + PauseMenu.prefab: 기지 구역(초대 버튼·공개 스테퍼) — 기지에서만 활성.
- `UI/MainMenuController`: Steam 분기 문구·대기 상태.
- 신규 `Net/SessionInfo`(NV) — docs/03 표.
- 입력 액션 `Invite`(Tab) — docs/01 입력 표 추가. `Player/PlayerInvite`(또는 PauseMenu에서 키 처리 — **PauseMenu.Update에서 처리**가 간단: 이미 Esc를 읽음. Player가 아닌 UI가 키를 읽는 건 InputFocus 규칙과 충돌 없음).
- docs/11·12 구현 현황.

### 하지 않는 것
- Facepunch 설치·트랜스포트·로비 생성·초대 수락 플로우(0-6). 방 이름/비밀번호(스코프 밖). 게임 중(InRun) 초대(docs/03 midgame 거부).

## 구현 순서 (0-6 완료 후)
1. `SessionInfo` NV + 호스트가 로비 생성 시 기본값 기록.
2. PauseMenu 기지 구역(초대 버튼·공개 스테퍼).
3. TeamStatus 헤더 분기 + Tab 초대.
4. 메인 메뉴 Steam 분기.
5. 검증(실계정 2대 — docs/03 수용 기준과 함께) → docs 갱신.

## 에디터 체크리스트
- 입력 액션 `Invite` 바인딩(Tab) 추가 — 인스펙터 또는 execute_code로 .inputactions 편집.
- 0-6 체크리스트(Steam AppID 480 실행 환경)는 그쪽 태스크.

## 테스트
- 로컬 모드(에디터, Steam 없음): 헤더 "로컬 모드 — 같은 PC에서만 참가", 일시정지에 초대 버튼 없음, 공개 스테퍼 비활성. 회귀: 기존 로컬 참가 정상.
- Steam 2대(AppID 480, 서로 친구): 호스트 Tab → 초대 오버레이 → 상대 수락 → Hub 스폰·팀 2/4·토스트 "OO가 들어왔어요". 호스트가 "초대만"으로 바꾸면 클라 헤더도 "초대만 공개"로. 친구 목록에서 "게임 참가"가 초대만일 때 막히는지(Steam 동작) 확인.
- 스테이지 진입 후 Tab → 무시(기지 전용).
