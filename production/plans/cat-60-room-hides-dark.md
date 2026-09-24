# 고양이 60 — 생성 방 안 은신처·어둠 구역 (2026-09-24, docs/10)

## 목표
생성 스테이지(부엌)에 창고 데모의 핵심 은신 요소 두 개를 넣는다: 숨을 곳(HideSpot — 상자·장화)과 어둠 구역(LightZone). 지금 부엌은 벽·상자·전리품뿐이라 들키면 숨을 데가 없다.

## 설계
- `RoomModule`에 `HideSpawns`(Transform[]) · `DarkZone`(Transform, 없으면 없음). 방 프리팹엔 표시만(중첩 NetworkObject 금지 — 고양이 58 규칙).
- 프리팹 3개(`Prefabs/Zones/`): `Hide_Box`(빈 상자, 2명, 1.8×1.5×1.8 + 입구 1.4m 앞 Box 고양이 스팟) · `Hide_Shoe`(장화, 1명, 1.1×1.4×2.2) · `DarkZone_4`(4×4, 바닥 어두운 판). 전부 루트 NetworkObject.
- `ZoneDefinitionSO`: `HideSpotPrefabs`(GameObject[]) · `DarkZonePrefab` · `DarkZoneChance`(0.6).
- `ZoneBuilder`: 방 뒤·고양이 전에 은신처(HideSpawns 전부, 시드로 종류)·어둠(확률) 스폰. 고양이가 스폰 때 스팟을 모으므로 상자 입구 스팟도 잡힌다.
- 배치(방마다 고정): 직선 NW 모서리 1·어둠 북쪽 출구 앞 / 모서리 NW 1·어둠 SE / 홀 NE·SE 2·어둠 SW / 보너스 NW 1 / 쥐구멍방·복도 없음 (문틈·가운데 상자 피함).

## 검증
- 시드 몇 개로 스폰 수·겹침(은신처가 벽·상자·문틈과 안 겹침), 고양이 스팟에 Box 포함.
- 1인: 은신처 E로 숨기 → Hidden, 어둠 구역 안에서 `LightZone.IsDark` true.
- 2인: 클라가 은신처에 숨기(OccupantCount 동기화), 어둠 판 보임.

## 결과 (2026-09-24)
| 항목 | 결과 |
|---|---|
| 겹침 | 방 × 소품 10조합: 벽·상자 겹침 0, 나오는 자리 막힘 0, 어둠 판 전부 방 안. 처음엔 8×8 방 모서리(-2.7, 2.7)가 45° 회전이라 상자 대각선이 벽을 뚫음 → (-2.5, 2.5) |
| 스트레스 | 시드 200개: 겹침 쌍 0·실패 0·NavMesh 끊김 0 (443ms) |
| 1인 | 실제 E로 빈 상자 숨기 → Hidden(1명) → E로 나옴 Active, 어둠 중심 IsDark true·3m 밖 false, 고양이 스팟 7개 중 Box 1 |
| 2인 (기지 출발) | 클라 NetworkObject 43/43, 숨을 곳 4·어둠 2, 클라 숨기 Hidden(1/2) → 나옴 Active, 예외 0 |

- 같이 한 것: ZoneTools 305줄 → 본체 272 + `.Stress` 77 + `.Props` 115 (partial, 300줄 규칙).
