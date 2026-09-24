# 고양이 134 — 씬에 놓인 전리품 디스폰 경고 (2026-09-25)

- 발견: 고양이 133 4인 루프(창고)에서 호스트 경고 1종 "[Netcode] Destroying in-scene network objects can lead to unexpected behavior. It is recommended to use NetworkObject.Despawn(false) instead." — 창고 씬에 놓인 치즈를 쥐구멍에 적립할 때 `DepositZone`이 `Despawn()`(= 파괴).
- 같은 길: 먹기(EdibleItem)·깨짐(CarryableItem.ServerBreak)·뇌물(CatBrain.Bribe)·유인 소모(CatLure).
- 고침: `Core/NetworkObjectExtensions.DespawnSafe()` — 씬 오브젝트는 `Despawn(false)`. 화면에서 끄기는 모두의 `OnNetworkDespawn`에서(`CarryableItem`, 캐리어 없는 캣닢·부스러기는 `CatLure`). 개발 로그 "씬 물건 끔"(스테이지 진행 중만 — 씬 언로드 일괄 디스폰은 안 찍음).
- 확인(2인, 새 빌드): 기지 → 창고 → 씬 치즈 적립 → 호스트: 디스폰·비활성, 경고 0 / 클라: "씬 물건 끔: loot_cheese_bit", 오류 0. 생성 맵(벽 속)은 전부 스폰 오브젝트라 전과 같음(파괴).
