# 10. 맵 생성 (W7–8) — 절차 생성이 아니라 "조합 랜덤"

## 개념

- 방 모듈(RoomModule 프리팹)을 **선형 체인**으로 이어 붙인다: 쥐구멍방 → 중간방 ×2~3 → (막다른 보너스방 0~1).
- 회전·연결은 DoorSocket 트랜스폼 정렬로만. 그래프 탐색·오버랩 해결 같은 일반 절차생성 문제를 피한다.
- 물리 배치(전리품 스폰)만 시드 랜덤 — "같은 방인데 물건이 다르다"로 리플레이성 확보.

## World/RoomModule.cs (프리팹 규약)

```csharp
public class RoomModule : MonoBehaviour
{
    public DoorSocket Entry;            // 이전 방과 접합
    public DoorSocket[] Exits;          // 1~2개 (2개면 분기 — 하나는 보너스방용)
    public Transform[] LootSpawns;      // 6~12개, LootTier 힌트 태그 포함
    public CatSpot[] CatSpots;          // 2~4개 — 종류(Look/Bed/Food/Sun/Groom…)·가중치. 방마다 Look ≥1, 존마다 Bed 1 (2026-09-24, docs/07)
    // CatSpots 중 Ambush(커튼·소파 밑 — 방마다 0~1, 꼬리가 보이는 자리)·Box(숨을 곳 입구 — 쥐와 고양이가 같은 상자를 두고 경쟁) 권장 (2026-09-24, docs/07)
    public HideSpot[] HideSpots;        // 1~2개 — 장화·빈 상자·커튼 등 쥐가 숨는 곳(정원 1~2). 없으면 추격의 탈출구가 달리기뿐 (2026-09-24, docs/07 Search)
    public Transform CatSpawn;          // 없으면 이 방엔 고양이 배정 안 함
    public Transform[] TrapSpawns;      // 2~4개
    public LightZone[] DarkZones;       // 어두운 영역 (07 시야 감쇠) — World/LightZone (2026-09-24): BoxCollider 트리거 + NetworkObject, 바닥 판 렌더러
    public NavMeshModifier[] navMods;   // 베이크 대상
}
```

### DoorSocket 정렬 규칙

- Entry/Exit는 문 중앙, +Z가 방 바깥을 향하게.
- 접합: `nextRoom.rotation = Quaternion.LookRotation(-exit.forward)` 정렬 후 `position += (exit.pos - entry.pos)`.
- **오버랩 검증**: 접합 후 방 바운즈(BoxCollider 1개를 모듈 루트에 규약으로) OverlapBox 체크 → 겹치면 해당 방 후보 스킵, 다음 후보. 5회 실패 시 체인 종료(짧은 존 허용). 이 단순 규칙이 오버랩 문제의 전부다 — 방 형태를 ㄱ자·ㄷ자로 만들지 않는 것이 모델링 규약 (직사각형만).

## Data/ZoneDefinitionSO.cs

```csharp
[CreateAssetMenu(menuName = "RatGame/Zone Definition")]
public class ZoneDefinitionSO : ScriptableObject
{
    public string ThemeId;                   // "kitchen", "basement"
    public GameObject RatHoleRoom;           // 시작방 (쥐구멍 포함) 프리팹
    public GameObject[] MiddleRoomPool;      // 테마당 4~6개 제작 (데모 기준)
    public GameObject[] BonusRoomPool;       // 1~2개 (고가치·고위험)
    public int MiddleRoomCount = 3;
    public int CatCount = 1;
    public SpawnTableSO LootTable;
    public TrapTableSO TrapTable;
    public float BonusRoomChance = 0.4f;
}
```

## Run/ZoneGenerator.cs (호스트 전용)

```csharp
public class ZoneGenerator
{
    // 반환: 스폰된 모든 NetworkObject (존 전환 시 일괄 디스폰용)
    public ZoneInstance Generate(ZoneDefinitionSO def, int seed, int zoneIndex);
    // 내부 순서:
    // 1) System.Random(seed + zoneIndex)
    // 2) RatHoleRoom 배치 → MiddleRoomPool에서 중복 없이 뽑아 체인 접합
    // 3) BonusRoomChance 판정 → 분기 소켓에 보너스방
    // 4) LootSpawns 채우기: SpawnTable 가중치 롤 (아래), 스폰 지점의 70%만 사용
    // 5) TrapSpawns: TrapTable 롤, 50% 사용. 쥐덫은 통로 폭 1m 미만 지점 우선
    // 6) CatSpawn 있는 방 중 랜덤 CatCount곳에 고양이
    // 7) 전 NetworkObject.Spawn → NavMeshSurface.BuildNavMesh()
}
```

### SpawnTableSO (가중치 테이블)

```csharp
[Serializable] public struct SpawnEntry { public LootItemSO Item; public int Weight; public int Max; }
```

- 존 인덱스 보정: `Tier weight × (1 + 0.3 × zoneIndex)` — Large/Special만 적용 (깊을수록 고가치).
- 스폰 높이 규약: LootSpawn 트랜스폼이 선반 위(y>1m)면 Large 금지 (내려올 수 없는 물건 방지 — 던져야 함).
- **반지(loot_ring)**: 고양이 Sleep 스팟 반경 2m 내 스폰 고정 — 고위험 고수익의 상징. Special 전용 규칙으로 하드코딩 허용.

## 구현 1단계 (2026-09-24, 고양이 57)

- `World/DoorSocket`(+Z 바깥, 폭 1.8) · `World/RoomModule`(루트 BoxCollider 트리거 = 바운즈, Entry·Exits·LootSpawns·TrapSpawns·CatSpawn·PlayerSpawns·RatHole) · `Data/ZoneDefinitionSO` · `Run/ZoneGenerator`(시드 → 쥐구멍방 → 중간방 `roomModulesPerZone` 3~4개 중복 없이 → 보너스방 확률, 접합은 yaw만 — FromToRotation은 180°에서 방을 눕혀서 SignedAngle로, 겹침은 수평 사각형 교차(여유 0.05m), 5번 버리면 끝, 방은 부모 기준 위치).
- 그레이박스 방 6종(`Tools/RatGame/Zone/Create Greybox Rooms` → `Prefabs/Rooms/Kitchen/`): 쥐구멍방 8×8 · 직선 8×10 · 모서리 8×8(옆 출구) · 홀 12×10(출구 2) · 복도 4×12 · 보너스 8×8(막다른). 벽은 NoiseBlocker(소리·시야 차단), 문틈 1.8m, 가운데 상자. `Data/Zones/Zone_Kitchen_Greybox`.
- ~~다음(2단계)~~ → 아래 2단계에서 구현: 방·스폰을 NetworkObject로, 호스트 런타임 NavMesh, 전리품·함정 테이블, 고양이 배치, RunManager 연결.

## 구현 2단계 (2026-09-24, 고양이 58)

- `Run/ZoneBuilder`(NetworkBehaviour + NavMeshSurface, 씬 오브젝트): 호스트가 스폰될 때 존을 만든다. 순서: 방 NetworkObject 스폰 → NavMesh 굽기(RoomStatic·NoiseBlocker 레이어만) → 쥐구멍(DepositZone) → 전리품 → 함정 → 고양이 → `PlayerPlacement.TeleportAllToSpawns`. 클라는 스폰을 받기만 한다.
- 방 프리팹은 정적 지오메트리 + 루트 NetworkObject만(중첩 NetworkObject 없음). 움직이는 건 전부 따로 스폰.
- `Data/SpawnTableSO`(항목·가중치·최대 개수, Large/Special 보정 `× (1 + DeepZoneBonusPerIndex(0.3) × 존 인덱스)`, 보너스방은 ×3에 스폰 지점 전부 사용) · `Data/TrapTableSO`. `ZoneDefinitionSO`에 LootTable·TrapTable·RatHolePrefab·CatPrefab.
- 규칙: 전리품은 LootSpawns의 70%, y>1m 지점엔 Large 금지. 함정은 TrapSpawns의 50%, 쥐구멍방엔 없음. 고양이는 CatSpawn 있는 방 중 CatCount곳(NavMesh 위로 보정).
- `RunManager.UpdateReturn`은 쥐구멍을 늦게 찾는다(생성 스테이지는 쥐구멍이 런매니저보다 늦게 생긴다).
- 에디터: `Tools/RatGame/Zone/Create Generated Stage` → 쥐구멍·함정 프리팹, `LootTable_Kitchen`·`TrapTable_Kitchen`, `Scenes/Stage_Generated`(빌드 세팅 등록). 그레이박스 색은 `Art/Materials/Greybox/`에 머티리얼 에셋으로 저장(프리팹은 메모리 머티리얼을 못 들고 있어 마젠타가 됐음).
- 아직: ~~방 안 HideSpot·LightZone~~(고양이 60), ~~기지 출발 발판 → 생성 스테이지~~(고양이 59 목적지 게시판), 존 전환, 반지 스폰 규칙.

## 구현 3단계 — 방 안 숨을 곳·어둠 (2026-09-24, 고양이 60)

- `RoomModule.HideSpawns`(모서리, 방 가운데를 봄 = 나오는 쪽) · `RoomModule.DarkZone`(중심). 방 프리팹엔 자리만.
- 소품 프리팹(`Tools/RatGame/Zone/Create Room Props (Hide·Dark)` → `Prefabs/Zones/`): `Hide_Box`(빈 상자 2명, 1.8×1.5×1.8, 입구 1.4m 앞 Box 고양이 스팟) · `Hide_Shoe`(장화 1명, 1.1×1.4×2.2) · `DarkZone_4`(4×4 LightZone, 바닥 어두운 판). 창고 데모와 같은 크기·인원.
- `ZoneDefinitionSO.HideSpotPrefabs`·`DarkZonePrefab`·`DarkZoneChance`(0.6). `ZoneBuilder`가 함정 뒤·고양이 전에 스폰(자리마다 시드로 종류, 어둠은 확률) — 고양이가 스폰 때 상자 입구 스팟을 모은다.
- 방별 자리: 직선 NW 1 + 어둠(북쪽 출구 앞) / 모서리 NW 1 + 어둠 SE / 홀 NE·SE 2 + 어둠 SW / 보너스 NW 1 / 쥐구멍방·복도 없음. 8×8 방 모서리는 45°로 놓이면 상자 대각선(1.27m)이 벽을 뚫어서 벽에서 1.5m 안쪽.
- 검증: 방 × 소품 10조합 벽·상자 겹침 0, 나오는 자리 막힘 0, 어둠 판은 전부 방 안. 시드 200개 스트레스 그대로(겹침·실패·끊김 0). 1인 실제 E로 숨기 → Hidden → E로 나옴, 어둠 중심 IsDark true / 3m 밖 false, 고양이 스팟 7개 중 Box 1. 2인(기지 출발): NetworkObject 43/43 클라 수신, 숨을 곳 4·어둠 2, 클라 숨기 Hidden(1/2) → 나옴 Active, 예외 0.

## 존 전환 (09 Transition 페이즈)

- 방식: **교체** — 이전 존 전체 디스폰 → 같은 씬에서 새 존 생성 → 플레이어를 새 쥐구멍으로 텔레포트.
- 연출: 화면 암전 1.5s + "더 깊은 곳으로..." 텍스트. (이어 붙이기는 메모리·NavMesh 재베이크 문제로 제외)
- 테마 전환: Zone1-2 = 부엌, Zone3+ = 지하실 (ZoneDefinitionSO 배열을 RunManager에 순서대로).

## 함정 테이블 (TrapBase 파생 4종 — 04 상태이상과 연결)

| 프리팹 | 효과 | 배치 규칙 |
|---|---|---|
| Trap_MouseTrap | 밟으면 Downed + 소음 80. 아이템 던져 격발 가능(1회성) | 좁은 통로 |
| Trap_GluePad | Trapped (동료 구출) | 전리품 스폰 주변 |
| Trap_WireShock | 접촉 Stunned 2s, 3s 주기 점멸 (꺼진 타이밍에 통과) | 통로 |
| Trap_Roomba | 경로 왕복, 충돌 시 밀쳐냄 + 소음 30. 위에 올라탈 수 있음(NavMeshObstacle) | 넓은 방 |

- 구현 (2026-09-24, 고양이 49): `World/TrapBase`(호스트 0.1s 폴링 — 박스 안 Active 쥐·풀린 물건) + `MouseTrap`(Downed + 소음 80 NoiseType.Trap, 1회성, 1s 안에 놓이거나 던져진 물건이면 헛격발) · `GluePad`(Trapped → 동료 E 1.5s 구출, 풀려난 쥐 4s 유예) · `WireShock`(켜짐 1.5s / 꺼짐 1.5s 반복, 켜진 동안 Stunned 2s + 소음 30). Roomba는 docs/13 컷 후보 + 집주인 로봇청소기(docs/09)로 대신. 데모 레이아웃에 3종(쥐 구멍 안쪽 쥐덫·끈끈이·창고방 문 앞 전기선). 시드 배치(TrapSpawns·TrapTable)는 존 생성기 태스크.

## 수용 기준 (W8)

- [x] 같은 시드 → 호스트·클라 동일 결과 (클라는 스폰 수신만이므로 자동 보장 — 검증만) (2026-09-24 고양이 58: 에디터 호스트 + macOS 빌드 클라, Stage_Generated. 스폰된 NetworkObject 31/31이 클라에게 보임, 방 4개 전부. 클라 쥐는 쥐구멍방 바닥(y 0.6, 쥐구멍 2.5m)에 섬. 출발 → 적립 12 → 둘 다 쥐구멍 → Returning → Returned → Hub(둘 다 접속). 호스트·클라 예외 0)
- [x] 시드 20개 연속 생성 스트레스 테스트: 오버랩 0, 생성 실패 0, NavMesh 구멍 0 (2026-09-24 고양이 57, `Tools/RatGame/Zone/Stress Test`: 시드 20개 방 4~6(평균 5.1)·보너스 12·겹침 0·실패 0·NavMesh 끊김 0·98ms, 시드 200개도 같음(942ms). 모서리방만으로 강제로 말리게 하면 겹침 후보 5번 버리고 4방에서 멈춤 — 겹침 0)
- [ ] 부엌 테마 그레이박스 방 4종으로 존 3개 연속 플레이
- [ ] 반지가 항상 고양이 옆에 있는지, 선반 위 Large 금지 규칙 확인
