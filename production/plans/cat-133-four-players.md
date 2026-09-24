# 고양이 133 — 4인 수용 기준 3개 + 시작 자리 겹침 (2026-09-25)

## 대상
- docs/12 "4인 플레이에서 TeamStatus·핑 동기화", docs/09 "파밍 → … → 기지 (4인)", docs/11 "Hub 4인 대기→레버→런→결과→Hub".

## 방법
- 에디터 호스트(Hub) + 빌드 클라 3 (차례로 `-autojoin`, 각자 로그). 팀 상태를 클라에서 보려고 `TeamStatusWidget`에 **바뀔 때만** 개발 로그 "팀 상태 연출: 헤더 | id 상태 …" 추가.
- 하네스: 네 명 핑(클라는 DevRemoteControl ping, 호스트는 TryPing) → client 3 Trapped 1.5s → Active → 네 명 발판(목적지 창고) → 적립 3개 → 네 명 쥐구멍 → Returned → Hub.

## 결과
| 항목 | 결과 |
|---|---|
| 핑 | 클라 3대 모두 0·2·3·4 핑 한 번씩 |
| 팀 상태 | 셋 다 "기지 · 4/4명", client 3 Trapped → Active 같은 순서 |
| 라운드 | 창고 13.7s 로드·4명 → 적립 14 → 4.3s 뒤 귀환(4/4) → 10.6s 뒤 Hub, 4명 유지, 누계 97523→97537(복구함) |
| 경고 | 호스트 1종: "[Netcode] Destroying in-scene network objects…" (창고 씬에 놓인 전리품을 적립 때 Despawn(true)) → 고양이 134 |

## 곁에서 찾은 버그 — 시작 자리 겹침 (고침)
- client 4가 들어오자 **호스트와 client 4가 동시에 Stunned**. `NetPlayerSpawner.GetSpawnPosition`이 `clientId % 자리 수(4)`라 client 4 → 0번 자리 = 호스트가 서 있는 곳. 겹쳐 생겨 밀쳐 튕겨 오르고 떨어져 낙하 기절. 실패한 접속(에디터 호스트 첫 시도)·재접속으로 번호는 계속 밀린다 — 실제 게임에서도 재접속하면 생김.
- 고침: 다른 쥐에게서 가장 먼 PlayerSpawn. 확인: 같은 번호 0·2·3·4로 4인 접속 → 기절 0, 가장 가까운 두 쥐 1.00m.
