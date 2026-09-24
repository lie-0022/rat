# 고양이 98 — 배관 물 보이기 (2026-09-25)

> 근거: 배관 물(97)이 토스트뿐이라 배관 안에서 뭐가 일어나는지 안 보인다.

- `World/PipeFlushView`(Wall_Pipe 루트): EventBus.HouseEvent Flush 시작 → 관 바닥 얕은 물(반투명 파랑, 콜라이더 없음) 켜고 출렁, 끝 → 끔. 알림은 이미 전원에게 가서 동기화 추가 없음.
- `Tools/RatGame/Zone/Add Pipe Water`: Wall_Pipe에만 물을 붙임(방 전체 다시 만들기의 fileID 소동 피함), `BuildPipe`도 같은 도우미. 머티리얼 `Art/Materials/PipeWater`.
- 확인: 1인 배관 입구에서 안을 보니 물 켜짐(전 False·중 True·후 False), 화면에 관 바닥 파란 물. 2인 클라 "배관 물 연출" 같은 배관, 예외 0.
- 곁가지 고침(따로 커밋): DEV 패널 FPS가 "0 · 11201ms"로 뜸 — 에디터 멈춤·로딩 프레임이 창에 섞임 → 1초 넘는 프레임 뺌.
