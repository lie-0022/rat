# 고양이 57 — 존 생성기 1단계: 방 모듈·접합·시드 (2026-09-24, docs/10, 태스크 2-1)

## 목표
docs/10의 "조합 랜덤" — 직사각형 방 모듈을 DoorSocket으로 **선형 체인** 접합(쥐구멍방 → 중간방 3~4 → 보너스방 0~1), 겹치면 다음 후보, 5번 실패하면 짧은 존. 1단계는 **네트워크 없이 배치·겹침·NavMesh 연결**까지 — 시드 20개 스트레스(docs/10 수용 기준 2)를 여기서 통과시킨다.

## 설계
- `World/DoorSocket` (표시용 MonoBehaviour, 태그 DoorSocket): 문 중앙, +Z가 방 바깥.
- `World/RoomModule`: Entry·Exits·LootSpawns·TrapSpawns·CatSpawn·PlayerSpawns, 루트 BoxCollider(트리거) = 방 바운즈. `Bounds`.
- `Data/ZoneDefinitionSO`: ThemeId, RatHoleRoom, MiddleRoomPool, BonusRoomPool, BonusRoomChance(0.4), CatCount — 방 개수는 BalanceConfig `roomModulesPerZone`(3~4, 기존).
- `Run/ZoneGenerator` (plain class): `Generate(def, seed, zoneIndex, parent)` → System.Random(seed+zoneIndex), 쥐구멍방 → 중간방 풀에서 중복 없이 뽑아 접합(entry.forward = -exit.forward, 위치 맞춤) → 바운즈 OverlapBox(다른 방 바운즈와, 0.05m 여유) 겹치면 다음 후보 → 5회 실패면 끝. 보너스방은 마지막 방의 남은 출구에 확률로. 결과 `ZoneLayout`(방 목록·사용한 출구·실패 수).
- 에디터 `Tools/RatGame/Zone/Create Greybox Rooms`: 그레이박스 방 6종 — 쥐구멍방(8×8, 출구 1) · 중간방 4(10×8 직선, 8×8 ㄱ출구(옆벽), 12×10 넓은 방(출구 2 — 하나는 보너스용), 6×12 긴 복도) · 보너스방(8×8, 막다른). 바닥·벽(RoomStatic)·문틈 1.8m·전리품/함정/고양이/쥐 스폰 표시. `Zone_Kitchen_Greybox` 정의 에셋.
- 에디터 `Tools/RatGame/Zone/Stress Test (20 seeds)`: 빈 임시 씬에서 시드 20개 생성 → 겹침 쌍 수·실패·방 수·NavMesh(베이크 후 쥐구멍방 → 모든 방 중심 경로 Complete)·소요 시간 로그.

## 다음 단계 (고양이 58~)
- 방·스폰을 NetworkObject로(클라는 스폰 수신만), 호스트 런타임 NavMesh 베이크, 전리품·함정 테이블 롤, 고양이 배치, RunManager 연결(새 스테이지 씬).

## 검증 결과 (2026-09-24)
- 시드 20개: 방 평균 5.1(4~6), 보너스 12, 겹침 버림 0, 겹침 쌍 0, 생성 실패 0, NavMesh 끊김 0, 98ms. 시드 200개: 평균 4.9, 보너스 70, 나머지 0, 942ms.
- 겹침 처리 강제 확인: 중간방을 모서리방 6개로(4번 꺾이면 제자리로 돌아옴), 방 6개 요청 → 겹침 후보 5번 버리고 4방에서 멈춤, 상대 위치 (0,0,0)(0,0,8)(8,0,8)(8,0,0) — 겹침 0.
- 버그 2개 고침: ① `Quaternion.FromToRotation`은 180° 반대 방향일 때 축을 멋대로 골라 방이 누울 수 있음 → 수평 SignedAngle yaw. ② 방을 월드 원점에 만들고 부모만 붙여서 스트레스 테스트 존이 스테이지 위에 겹쳐 생김(NavMesh 섞일 위험) → 부모 기준 위치.
