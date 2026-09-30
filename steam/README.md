# 스팀 업로드 (docs/13 3-6)

데모 빌드를 Steam에 올리는 설정. **여기 있는 건 틀뿐이고, 실제 업로드는 사람이 한다.** (고양이 321)

## 한 번만 — 사람이 할 일

1. Steamworks 파트너 계정에서 데모 앱을 만든다 → **AppID**와 Windows 디포 **DepotID**를 받는다.
2. 두 숫자를 채운다.
   - `app_build_demo.vdf`: `"AppID"`, `"Depots"` 안의 `"0000001"`
   - `depot_build_windows.vdf`: `"DepotID"`
3. 게임 코드의 AppID도 바꾼다: `Assets/_Project/Scripts/Net/SteamLobbyService.cs`의 `DevAppId = 480`
   (480은 Valve 시험용 Spacewar — 지금은 이걸로 초대·로비를 시험한다).
4. steamcmd 설치: `brew install --cask steamcmd` (또는 Steamworks SDK의 `tools/ContentBuilder/builder_osx`).
   업로드용 Steam 계정은 파트너 권한이 있어야 하고, 첫 로그인 때 Steam Guard 코드를 묻는다.

## 도전과제 (고양이 326)

게임이 로컬 도전과제를 깨면 같은 이름의 Steam 도전과제를 켠다(시작할 때 이미 깬 것도 맞춤). Steamworks → 통계 및 도전과제에 **아래 API 이름 그대로** 등록한다. 개발 AppID 480에선 꺼져 있다.

<!-- achievements:start -->
`ach_chef_enemy` `ach_collector` `ach_deep_rat` `ach_egg_courier` `ach_first_extract` `ach_ghost` `ach_medic` `ach_survivor`
<!-- achievements:end -->

(이 목록은 EditMode 시험이 실제 도전과제와 같은지 검사한다 — 도전과제를 더하면 여기도.)

## 올릴 때마다

1. 유니티: **Tools → RatGame → Build → Windows x64 Demo (release)**
   → `Builds/Release/Windows/` (Windows Build Support 모듈이 있어야 한다 — 없으면 메뉴가 이유를 말하고 멈춘다)
2. 올리기 전 연기 시험: 윈도 PC에서 `Builds/Release/Windows/Rat.exe -smokehost -unitytransport` → 창이 스스로 호스트·기지·스테이지 1을 돌고 꺼진다. `Player.log`에 "스모크 통과"가 있어야 한다(맥 릴리스는 유니티 메뉴 Run Release Smoke).
3. 미리 보기 (`"Preview" "1"` 그대로): 올리지 않고 올라갈 파일 목록만 만든다 → `Builds/SteamOutput/`에서 확인.
   ```bash
   steamcmd +login <업로드 계정> +run_app_build "$(pwd)/steam/app_build_demo.vdf" +quit
   ```
4. 목록이 맞으면 `"Preview"`를 `"0"`으로 바꾸고 같은 명령 → 업로드.
5. Steamworks 웹 → 앱 → SteamPipe → 빌드에서 올라간 빌드를 브랜치(먼저 비공개 `beta`)에 연결한다.
   `"SetLive"`를 비워 둔 건 실수로 바로 공개되지 않게 하려는 것.

## 싣지 않는 것

- Burst 디버그 기호: 빌드 메뉴가 `Builds/Symbols/Windows-{버전}`으로 옮긴다(크래시 분석용으로 보관). 디포 설정에도 한 번 더 뺐다.
- `*.pdb`, IL2CPP 백업 폴더.

## 아직 안 한 것 (판단·사람)

- 회사 이름·제품 이름(`ProjectSettings` companyName `DefaultCompany`, productName `Rat`): 바꾸면 세이브 폴더 위치가 바뀐다
  (`~/Library/Application Support/DefaultCompany/Rat` → 새 이름). 정식 이름이 정해지면 옛 세이브 옮기기와 같이.
- 스팀 없이 켰을 때 스팀으로 다시 띄우기(`SteamClient.RestartAppIfNecessary`): 데모 AppID가 생긴 뒤.
