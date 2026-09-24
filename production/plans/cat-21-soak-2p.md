# 고양이 21 — 2인 소크 테스트 (쥐 도구 포함) (2026-09-24)

## 목표
고양이 20 소크에서 안 나온 상태(Curious·Track·Search·Toy·BoxSit·Distracted·Blunder)를 한 판 안에서 섞어 돌린다. 2인이어야 Toy가 되고, 쥐가 도구를 써야 호기심·냄새·수색·미끄러짐이 나온다.

## 방법
- 에디터 호스트 + macOS 빌드 클라(`-unitytransport -autojoin -autowander` — 클라 쥐 자동 배회). 호스트 쥐도 자동 배회. timeScale 2(호스트).
- 호스트가 실시간 15s마다 "쥐 짓"을 돌아가며: ① 고양이 앞 치즈 굴리기 ② 고양이 진행 방향에 비누 ③ 호스트 쥐가 가까운 숨을 곳에 숨었다 8s 뒤 나옴 ④ 고양이 옆 접시 깨기 ⑤ 호스트 쥐가 치즈 들고 배회(냄새) ⑥ 털실 대신 ServerDistract(캣닢 흉내 — 비틀거림).
- 180s 실시간. 집계는 고양이 20과 같음(ServerStateChanged 이벤트, 갇힘, 반복 루프, 예외) + 클라 Player.log 예외.

## 결과 (2026-09-24)

### 1차 (게임 381s, 게으름뱅이)
- 예외 0(호스트·클라), 갇힘 0. 상태 12종: Curious·BoxSit·Toy·Distracted·Blunder(Wobble)·Zoomies·Ambush 포함.
- **루프 1건**: 매복 반복(256s). 원인 — 고양이 20 수정은 "방금 고른 스팟"만 막았는데, **복귀(NearestSpot)로 도착한 매복 스팟은 최근 목록에 안 들어가** 다음 복귀가 또 그 스팟을 골랐다. 수정: `ArriveAtSpot`에서 도착한 스팟을 항상 최근 목록에 넣는다.

### 2차 (수정 후, 게임 514s, 게으름뱅이)
| 상태 | 횟수 | 최장(게임 s) |
|---|---|---|
| Chase | 13 | 7 |
| Return | 11 | 10 |
| Suspicious | 10 | 3 |
| Capture | 10 | 0 |
| Toy | 9 | 14 |
| Patrol | 8 | 57 |
| Track | 5 | 20 |
| Distracted | 2 | 3 |
| Blunder:Wobble | 2 | — |
| Zoomies | 2 | 1 |
| Search | 1 | 4 |
| Sleep | 1 | 90 |
- **예외 0(호스트·클라), 갇힘 0, 반복 루프 0.**
- 두 소크(고양이 20·21) 합쳐 미커버 상태: Away·Fight는 20에서, Curious·BoxSit·Ambush는 21-1차에서 확인. Blunder Slip·Stun·Startle·Flee·Hairball은 이 셋업에서 우연히 안 걸림(단독 검증 완료).
