# 01. 프로젝트 셋업 (W1)

## Unity 버전·렌더 파이프라인

- **Unity 6000.0 LTS** (6000.0.x 최신 패치). 이유: Multiplayer Play Mode(멀티 테스트 필수), NGO 2.x 정식 지원.
- **URP** (Universal Render Pipeline). 로우폴리+어두운 실내 라이팅에 충분, 빌드 가볍다.
- 컬러 스페이스: Linear. 텍스처는 Point 필터(픽셀 텍스처 스타일).

## 패키지 목록 (Package Manager / manifest.json)

| 패키지 | 버전 | 용도 |
|---|---|---|
| com.unity.netcode.gameobjects | 2.x 최신 | 넷코드 본체 |
| com.unity.transport | NGO 의존 | 로컬 테스트용 UnityTransport |
| com.unity.multiplayer.playmode | 최신 | 에디터 내 2~4인 테스트 |
| com.unity.inputsystem | 1.x | 입력 |
| com.unity.cinemachine | 3.x | 3인칭 카메라 |
| com.unity.ai.navigation | 2.x | NavMesh (고양이) |
| Facepunch.Steamworks | NuGet/DLL 수동 | Steam 로비·릴레이 |
| netcode.transport.facepunch | GitHub (community) | NGO용 Steam 트랜스포트 |

주의: Facepunch 트랜스포트는 W2에 붙인다. W1~2 개발·테스트는 UnityTransport + Multiplayer Play Mode로 진행 (03-netcode 참조).
Steam AppID는 확보 전까지 **480 (Spacewar)** 사용.

## 폴더 구조 (Assets/)

```
Assets/
  _Project/                    ← 우리 에셋 전부 여기. 스토어 에셋과 격리
    Scenes/
      Boot.unity               ← 부트스트랩 전용 (항상 빌드 0번)
      MainMenu.unity
      Hub.unity                ← 쥐구멍 허브 (로비 겸용)
      Run_Kitchen.unity        ← 구역 생성용 베이스 씬 (테마별 1개)
      Run_Basement.unity
      Sandbox_Net.unity        ← 넷코드 검증용 회색 박스 씬 (W1)
    Scripts/                   ← 02-architecture의 네임스페이스 구조와 1:1
    Prefabs/
      Player/  Items/  Cat/  Traps/  Rooms/Kitchen/  Rooms/Basement/  UI/  Net/
    Data/                      ← ScriptableObject 에셋
      Items/  Zones/  Equipment/  Skins/  Codex/  SpawnTables/  Balance/
    Art/                       ← 엄지성 작업물 (Models/ Textures/ Animations/)
    Audio/
    Settings/                  ← URP 에셋, Input Actions
  Plugins/                     ← Facepunch DLL 등
```

## 씬 로딩 규칙

- Boot 씬의 `GameBootstrap`이 유일한 진입점. DontDestroyOnLoad 매니저 생성 후 MainMenu 로드.
- 런 씬은 NGO의 NetworkSceneManager로 호스트가 로드 → 클라 자동 동기화.
- 방 모듈은 씬이 아니라 **프리팹** — 런 씬 로드 후 ZoneGenerator가 스폰 (10-map-generation).

## Input Actions (Settings/RatInput.inputactions)

| 액션 | 키보드/마우스 | 게임패드 | 타입 |
|---|---|---|---|
| Move | WASD | 왼스틱 | Value/Vector2 |
| Look | 마우스 델타 | 오른스틱 | Value/Vector2 |
| Jump | Space | A | Button |
| Sprint | Shift | L3 | Button(hold) |
| Crouch | Ctrl | B | Button(toggle 옵션) |
| Grab | 마우스 좌클릭 | RT | Button(hold=잡기 유지) |
| Throw | 마우스 우클릭(홀드 차지) | LT | Button |
| Interact | E | X | Button |
| Squeak | Q | D-Pad Up | Button |
| Ping | 마우스 휠클릭 | R3 | Button |

## 물리 설정 (Project Settings)

- Fixed Timestep: 0.02 (50Hz). 물리 게임이므로 낮추지 않는다.
- 레이어: `Player, Carryable, Cat, Trap, RoomStatic, InteractTrigger, NoiseBlocker, Ragdoll`
- 충돌 매트릭스: Ragdoll↔Player off (다운된 몸이 산 플레이어를 밀지 않게), Cat↔Carryable on (고양이가 물건을 침).
- Player Rigidbody: mass 1, drag 1, freezeRotation XZ. Carryable: 아이템별 mass (05 문서 표).

## 태그

`RatHole`, `LootSpawn`, `CatSpawn`, `PlayerSpawn`, `DoorSocket` (방 연결용, 10 문서).

## 수용 기준 (W1 종료 시)

- [ ] 빈 프로젝트에 위 패키지 전부 설치, 컴파일 에러 0
- [ ] Boot→MainMenu→Sandbox_Net 씬 플로우 동작
- [ ] Input Actions 에셋 완성, 테스트 스크립트로 전 액션 로그 확인
- [ ] Multiplayer Play Mode로 에디터 2인스턴스 실행 확인
