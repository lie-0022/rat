# 고양이 99 — 접속 재시도의 Lobby → Lobby 오류 (2026-09-25)

- 무엇: `-autojoin`의 첫 접속이 늦으면 재시도에서 `JoinAsync`가 다시 Lobby로 전이 → "잘못된 상태 전이: Lobby → Lobby" Error 로그(소크 로그를 흐림, 고양이 94에서 봄).
- 고침: `NetworkLauncher.JoinAsync` — 이미 Lobby면 전이하지 않음.
- 확인: 소크 5 클라 로그 "Lobby → Lobby" 0.
