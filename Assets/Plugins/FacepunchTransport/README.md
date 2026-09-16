# Facepunch Transport (vendored)

- 출처: https://github.com/Unity-Technologies/multiplayer-community-contributions
  `Transports/com.community.netcode.transport.facepunch` @ `0fab63847037` (MIT, LICENSE.md)
- 패키지로 설치하지 않은 이유: 패키지에 든 Facepunch.Steamworks 2.3.2의 macOS 네이티브 라이브러리가
  x86_64 전용이라 Apple Silicon 에디터에서 로드되지 않음. Steamworks 라이브러리는
  `Assets/Plugins/Facepunch.Steamworks` (2.5.2, universal dylib)를 쓴다.
- RatGame 수정 사항
  1. `Shutdown()`에서 `SteamClient.Shutdown()` 제거 — Steam 수명은 `RatGame.Net.SteamLobbyService`가 관리.
  2. asmdef: 이름 `Netcode.Transports.Facepunch`, 참조는 `Unity.Netcode.Runtime`만
     (원본의 `Unity.Networking.Transport.NetcodeInterop`는 코드에서 쓰지 않고 이 프로젝트에 없음),
     플랫폼은 Editor·macOS·Win64.
