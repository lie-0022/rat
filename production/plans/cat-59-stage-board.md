# 고양이 59 — 기지 목적지 게시판 + 생성 스테이지 2인 낙하 버그 (2026-09-24)

## 목표
기지에서 생성 스테이지(부엌)로 실제 출발할 수 있게. 창고 데모 맵은 고양이 기능 전시라 그대로 두고 **고를 수 있게** 한다 (기본 창고).

## 구현
- `World/DeparturePad` — `_stageScenes`/`_stageNames`, `Destination` NV, `ServerCycleDestination`(카운트다운 중 불가), 출발 씬을 목록에서.
- `Run/RunSession.StageChoice` — 기지 씬이 귀환마다 새로 로드돼도 선택 유지.
- `World/StageBoard` — E(즉시)로 목적지 돌리기, 글씨는 NV를 읽어 각자 그림.
- `UI/RunBar` — 기지 보조 줄에 목적지.
- `Editor/HubTools` — `Tools/RatGame/Hub/Add Stage Board` (Hub 동쪽 벽 앞, 다시 실행하면 새로). Editor asmdef에 TextMeshPro 참조.

## 찾은 버그 (2인에서만 보임)
기지 → 부엌 출발 시 **클라 쥐가 허공으로 떨어짐** (y −18 → −28).
- 진단: 텔레포트 RPC에 "발밑 바닥 있음/없음" 로그 → 클라는 스테이지 도착 텔레포트 때 바닥 없음. 클라 로그에 NGO `[Deferred OnSpawn] ... NetworkObject was not received within the timeout` 경고 7개 — **씬 로드 중(ZoneBuilder.OnNetworkSpawn) 스폰한 방·전리품이 로딩 중인 클라에 안 감**. 직접 플레이 테스트(고양이 58)는 클라가 존이 다 생긴 뒤 접속해서 안 보였다.
- 고침: `ZoneBuilder`가 `RunSession.DepartPending`이면 `OnLoadEventCompleted` 뒤에 짓는다(docs/03이 원래 그렇게 적어 둠).
- 안전망: `PlayerController` 텔레포트 바닥 대기(바닥 생길 때까지 제자리, 5s 상한)·낙하 구조(y<-10 → 가까운 PlayerSpawn). 버그 상태 빌드에서 두 경로 모두 실제로 탔다(대기 시간 초과 → 낙하 → y -10에서 시작 위치로, Hub 도착 때 "바닥 생김").

## 검증 (에디터 호스트 + macOS 빌드 클라)
| 항목 | 결과 |
|---|---|
| 게시판 E | 실제 E 입력 → 목적지 창고 → 부엌(생성) |
| 출발 | 둘 다 발판 → Stage_Generated, 존: 방 5·전리품 23·함정 2·고양이 1 |
| 클라 수신 | NetworkObject 35/35, 클라 쥐 y 0.6(쥐구멍 2.5m), 바닥 대기·낙하 로그 0, Deferred 경고 0 |
| 라운드 | 적립 12 → 쥐구멍 → Returning → Returned → Hub(2명) |
| 목적지 유지 | 귀환 뒤 기지 게시판 "부엌(생성)" |
| 예외 | 호스트 0 · 클라 0 |

- save.json 누계가 97523→97535로 바뀌어 원본으로 되돌림.
