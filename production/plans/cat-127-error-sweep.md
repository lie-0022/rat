# 고양이 127 — 오류·경고 훑기 (2026-09-25)

## 왜
- 고양이 126에서 쓰러진 동안 콘솔 오류가 초당 ~10번 찍히던 걸 우연히 발견(고양이 46부터 있었음). 지금까지 소크는 **예외만** 세서 Error·Warning 로그를 못 봤다.
- 그래서 한 번 **메시지 종류별로** 모아 본다: 호스트(에디터) + 빌드 클라 1(`-autowander`), 스테이지 5를 오늘의 집 7가지로 차례로 다시 열고, 각각 상태이상(기절·덫·쓰러짐) → 집주인 이벤트 9가지(3초 간격) → 고양이를 내 앞으로. 쓰러짐 단계 밖에선 쓰러진 쥐를 바로 살려 전멸로 안 끝나게.

## 찾은 것

| 어디 | 메시지 | 횟수 | 원인 | 고침 |
|---|---|---|---|---|
| 호스트 | NetworkVariable is written to, but doesn't know its NetworkBehaviour yet | 96 (방마다·로드마다) | `GridRoom.ServerSetOpenSides`가 스폰 전에 `.Value`를 씀 | 스폰 전이면 초기값을 가진 새 NV로 바꿔 끼움(스폰 때 그 값이 클라로 감), 스폰 뒤면 `.Value` |
| 클라 | Failed to create agent because there is no valid NavMesh | 23 (고양이마다·로드마다) | 고양이 프리팹의 NavMeshAgent가 켜진 채 생김 — 클라엔 NavMesh가 없음(고양이 16에서 "무해"로 남겨 둠) | 프리팹 에이전트 끔, 호스트만 `CatBrain.OnNetworkSpawn`에서 켬. 고양이 생성 도구(CatTools)도 끈 채로 |
| 클라 | [Netcode] [GetPlayerNetworkObject] Only the server can find player objects from other clients | 7 (남이 쓰러질 때마다) | `DownedBody.ApplyColor`가 서버 전용 API로 주인 쥐를 찾음 → 클라에선 못 찾아 기본 팀색으로 칠함(털 색 팔레트가 다운 몸에 안 나옴) | 스폰 목록에서 `IsPlayerObject && OwnerClientId`로 찾기 |

예외는 호스트·클라 모두 0.

## 고친 뒤 다시 (없음·분주, 빌드 새로)
- 호스트 오류·경고 0종, 클라 0 (접속 재시도 "Failed to connect to server." 3줄 빼고).
- 고양이 3/3 에이전트 켜짐·NavMesh 위, 움직임 확인. 클라: 다운 몸 연출 5번 오류 없음, 배관 입구 연출 6(열린 면 NV가 클라에 제대로 감).

## 곁가지 관찰 (안 고침)
- 에디터 플레이 직후 빌드 클라 `-autojoin`이 처음 몇 번 "Failed to connect"하고 재시도로 붙는다. 실패한 시도가 호스트에 유령 연결로 남기도 함(연결 수 3) — 유령 정리(고양이 67)·로드 게이트(121)가 처리. 개발 도구 쪽이라 그대로.
