# 10. 맵 생성 (W7–8) — 절차 생성이 아니라 "조합 랜덤"

## 생성기 v2 — 격자 그래프 "벽 속" (2026-09-24 사용자 결정, 목표)

> 맵 시안 "벽 속"(`design/references/2026-09-24-map-draft-inside-walls.png`)처럼 **갈래·고리·막다른 방**이 있는 맵. 아래 v1(선형 체인)은 부엌 테마로 남는다.
> 여전히 "조합 랜덤" — 방·통로·막음벽은 전부 에디터에서 만든 프리팹, 실행 중엔 고르고 놓기만 한다(절차 지형 없음, docs/00).

- **격자**: 칸 한 변 `cellSize`(14m). 방 하나 = 칸 하나(방 크기 ≤ 12×12, 칸 가운데). 이웃 칸끼리만 연결 → 겹침 검사가 필요 없다.
- **방 모듈(GridRoom)**: 네 면 가운데 문틈(1.8m) + 면마다 막음벽. 연결된 면만 막음벽을 끈다 — `OpenSides` NetworkVariable(비트 4개)로 클라도 스폰 때 같은 모양. 크기 여러 종(6×6·8×8·12×6·6×12·12×10…). 회전 안 함(가로·세로형을 따로 둠).
- **통로(GridCorridor)**: 이웃 두 방의 마주 보는 문틈 사이. 길이 = 칸 − 두 방 반폭. 보통 1.8m 폭, **파이프**(고리를 닫는 긴 연결)는 1.2m 폭 — 시안의 가는 통로.
- **그래프 순서** (시드):
  1. 출발 칸 (0,0) = 출발방(벽 속 입구, PlayerSpawn).
  2. **주 경로**: 겹치지 않는 무작위 걸음 `mainPathLength`칸 → 끝 칸 = **목적지방**(상점·식량 창고, docs/09 새 루프).
  3. **갈래**: 주 경로 방마다 `branchChance`로 빈 이웃 칸에 1~`branchMaxDepth`칸 곁가지, 끝 = 막다른 방(전리품·위험).
  4. **고리**: 주 경로에서 인덱스가 `loopMinGap` 이상 떨어진 두 방이(또는 그 곁가지가) 이웃 칸이면 파이프로 1개 연결 → 목적지까지 두 길.
  5. 칸마다 방 종류는 시드로(출발·목적지는 전용). 이후는 v1과 같다: NavMesh 굽기 → 전리품·함정·숨을 곳·어둠·고양이.
- **검증**: 시드 200개 — 출발 → 목적지 NavMesh 경로 있음, 모든 방 도달, 고리 비율, 방 수 분포, 시간.
- **구현 (2026-09-24, 고양이 62)**: `Run/GridLayoutPlanner`(칸 그래프 — 고리가 자연히 안 생기면 빈 칸 1~3개 샛길로 닫음) · `World/GridRoom`(막음벽 4개 + `OpenSides` NV) · `Run/GridZoneLayout`(배치) · `Run/GridZoneBuilder`(네트워크 스폰·NavMesh·채우기, 식량 창고를 목적지방 Depot에) · `Run/ZonePopulator`(v1과 공유하는 채우기) · `Data/GridZoneSO`(`Zone_Walls`, 채우기 테이블은 부엌 것). 에디터: `Tools/RatGame/Zone/Create Wall Rooms`·`Create Wall Stage`(씬 `Stage_Walls`, 기지 게시판 "벽 속(생성)")·`Preview Wall Layout`·`Grid Plan Stress`.
  - 결과: 시드 2000개 실패 0·고리 93%·방 14.5개(8~23), 미리보기 겹침 0·출발→목적지 경로 OK. 플레이 한 판: 방 12·통로 12·전리품 56~60개(가치 2000~2800)·함정·숨을 곳·어둠·고양이. 2인(기지 출발): 클라 NetworkObject 109/109, 막음벽 상태 12/12 방 동기화, 두 쥐 출발방 문 쪽을 봄(오차 0°), 목적지 창고에 모이면 → 귀환 → 기지.
  - **깊이별 난이도 (고양이 66, 잠정)**: `Zone_Walls.CatCountByStage` {1,1,2,2,3}·`TrapRatioByStage` {0.4…0.8}(배열 끝을 넘으면 마지막 값), 전리품 깊은 존 보정 = 스테이지 − 1. `ZonePopulator.CatCountOverride/TrapRatioOverride`. 확인: 스테이지 1 고양이 1·함정 9/21 → 스테이지 5 고양이 3(성격 제각각)·함정 16/17, 12초 동안 셋 다 순찰로 ~20m 이동, 예외 0.
  - **보물방 (고양이 84, 잠정)**: 막다른 방 중 출발방에서 가장 먼(칸 그래프 거리) 한 곳 = 보물방 — ZonePopulator 보너스방 규칙(전리품 자리 전부, 보너스방을 **먼저** 채움 — 대형 맵 상한 때문)에 대형·특수 ×`TreasureBigWeight`(8, v1은 3) + 첫 자리는 거의 확실히 대형·특수, 함정 자리도 전부(`TreasureTrapRatio` 1). 10맵: 보물방 평균 669(보통 방 213), 맵 식량 총량은 그대로. 맵 시안 "큰 방마다 막다른 작은 방(전리품·위험 방 자리)". 하나만 — 할당량이 이미 쉬운 편(판단 부탁 7)이라 전부 보물방으로 하면 식량이 너무 늘어서. 스테이지 안내에 "막다른 방 하나는 보물방" 줄.
  - **보물방 불빛 (고양이 88)**: 보물방 가운데 `Treasure_Glow`(네트워크 오브젝트, 콜라이더 없음) — 치즈 노랑 점광(세기 6±2 일렁임 `World/TreasureGlint`, 범위 9m, 그림자 없음) + 금빛 부스러기 5개. 낮 햇빛 아래서도 옆 방 문틈으로 노란 빛이 보인다. 목적지방 불빛(복숭아빛, 가만히)과 색·움직임으로 구분. `Tools/RatGame/Zone/Create Treasure Glow` → `Zone_Walls.TreasureGlow`.
  - **배관 입구 매복 (고양이 115)**: 배관 양쪽 입구 방 쪽 0.6m·옆 1m에 `Ambush` 관찰점(`Zone_Walls.PipeAmbushWeight` 0.5) — 고양이가 가끔 쥐 구멍 옆 벽에 붙어 매복(기존 매복 그대로: 60초, 꼬리만 보임, 2m 안에서 보이면 덮침, 소리를 들으면 평소처럼 의심). 벽 속엔 커튼 같은 매복 자리가 없어서 매복이 한 번도 안 나왔던 것. 스팟은 고양이 스폰(채우기) 전에 만들어야 고양이가 모은다.
  - **맵 불변 검사 (2026-09-25 고양이 114)**: 스테이지 5를 무작위 시드로 25번 지어서 — 보물방이 출발·목적지 아님, 문지기면 초소 있음, 순찰꾼 길 지점 ≥2, 역할 겹침 없음, 고양이가 모든 방 도달, 출발방에서 킁킁 경로 성공 → 실패 0. 문지기 25·순찰꾼 25·아기 15·배관 25, 오늘의 집 다섯 가지 모두 나옴.
  - **오늘의 집 (고양이 106, 잠정)**: 스테이지 2부터 `Zone_Walls.ModifierChanceByStage` {0,0.5,0.6,0.7,0.8} 확률로 하나(같은 비중): 정전(어둠 구역 확률 `BlackoutDarkChance` 0.9) · 덫 대방출(함정 비율 +`TrapSaleBonus` 0.2) · 고양이 간식 날(고양이 이동 ×`TreatsCatSpeed` 0.85) · 집주인 외출(집주인 사건 0) · 분주한 집(집주인 사건 +`BusyExtraEvents` 2) · 고양이 손님(고양이 +1, 고양이 117 — 이제 여섯 가지 같은 비중). 스테이지 안내 머리줄에 "오늘: 정전" 식으로. 씨앗(시드)으로 정해서 같은 맵이면 같은 조건.
  - **순찰꾼 (고양이 92, 잠정)**: 문지기·아기가 아닌 어른 고양이가 남으면 `Zone_Walls.PatrolChanceByStage` {0,0,0,0.5,1} 확률로 순찰꾼 — 출발→목적지 최단 경로(배관 제외 BFS)의 가운데 방들에 `PatrolPoint` 스폰 뒤 `CatBrain.ServerMakePatroller`. docs/07 "순찰꾼 고양이". 스테이지 안내 고양이 줄에 합쳐짐(플래그 16).
  - **문지기 (고양이 80, 잠정)**: 어른 고양이 2마리 이상인 스테이지에서 `Zone_Walls.GuardChanceByStage` {0,0,1,1,1} 확률로 한 마리(아기 아님)를 문지기로 — 목적지방의 첫 열린 면 너머 이웃 방 문 안쪽 1.5m에 `GuardPost` 관찰점 스폰 뒤 `CatBrain.ServerMakeGuard`. docs/07 "문지기 고양이".
  - **쥐 전용 배관 (고양이 79)**: 고리를 닫는 연결(IsPipe)은 폭 1.2m·높이 1.4m 배관 — 쥐(키 1.2m, 눈 1.05m)는 서서 지나가고, 고양이 NavMesh(필요 높이 2m)는 안 이어진다. 방 쪽 문틈엔 막음벽 대신 **배관 입구 판**(아래 가운데 1.2×1.4 구멍). 고양이 길은 나무(주 경로·곁가지)만으로 모든 방에 닿는다 — 배관은 고리에만 있어서. 쫓기면 배관으로 빠져 고리를 돌아 떼어 낼 수 있다. `GridRoom.OpenSides` 위 4비트 = 배관 면(새 NV 없음). 들고 있는 큰 물건은 배관에 걸릴 수 있음(웃기면 사양).
  - **아기 고양이 (고양이 75, 잠정)**: 고양이 2마리 이상인 스테이지에서 `Zone_Walls.KittenChanceByStage` {0,0,0.5,0.5,0.6} 확률로 한 마리를 아기(`CatBrain.ServerMakeKitten`)로 — 엄마·아기 관계는 CatRelation이 붙인다(아기가 놀라면 냐앙 → 어른 고양이가 달려옴). 확인: 스테이지 5 고양이 3 → 한 마리 아기(몸 0.6), 관계 MotherKitten, 아기를 의심 상태로 만들자 어른 둘 다 Respond, 예외 0.
  - **벽 속 장식 (고양이 73)**: 방마다 나무 샛기둥(1.4m 간격, 문틈 피함)·천장 배관 1줄(높이 2.15)·분홍 단열재(면마다 50%). 콜라이더 없음·Default 레이어라 NavMesh(RoomStatic·NoiseBlocker만 구움)와 물리에 영향 없음. 미리보기 도구도 같은 레이어만 굽도록 맞춤(전엔 장식 렌더 메시까지 구워 "못 가는 방"이 거짓으로 나왔음) + 목적지는 안전지대라 문 밖까지로 검사.
  - **집주인 이벤트 (고양이 68)**: 벽 속 맵엔 TV·창문·청소기가 없어 부르기·밥·불 켜기(어둠 구역)만 뽑힌다(HouseEventDirector가 있는 것만 고름). 부르기용 **Door 스팟**을 소(6×6)·대(12×12) 방에 추가 — "집 쪽 구멍". 확인: 문 스팟 4개 맵에서 부르기 → 고양이가 (-41,-1) 문으로 나가 37s 뒤 다른 문(-13,-29)으로 돌아옴.
  - 시작 방향: 1인칭 시선이 몸 방향을 정해서 텔레포트 회전만으론 안 돈다 → `PlayerController.FaceClientRpc`(배치·개발 메뉴 이동 때만).

| 수치 (ZoneDefinitionSO — 테마 데이터) | 기본값 |
|---|---|
| cellSize | 14 |
| mainPathLength | 5~7 |
| branchChance / branchMaxDepth | 0.6 / 2 |
| loopMinGap | 3 |

## 개념 (v1 — 부엌 테마)

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
| Trap_MouseTrap | 밟으면 Downed + 소음 80. 아이템 던져 격발 가능(1회성). **미끼**(고양이 129): 벽 속에선 `GridZoneSO.TrapBaitChance` 0.5로 같은 방(벽에 안 막힌 6m 안) 작은 음식을 판 위로 옮김 — 서서 집으면 격발 + 기절 `trapBaitStunSeconds` 2.5 + 떨어뜨림, 웅크려 집으면 무사, 미끼가 판 밖으로 밀리면 헛격발. 처음 3m(`trapBaitHintMeters`) 안에 오면 한 번 규칙 안내(고양이 132, 각자 계산) | 좁은 통로 |
| Trap_GluePad | Trapped (동료 구출) | 전리품 스폰 주변 |
| Trap_WireShock | 접촉 Stunned 2s, 3s 주기 점멸 (꺼진 타이밍에 통과) | 통로 |
| Trap_Roomba | 경로 왕복, 충돌 시 밀쳐냄 + 소음 30. 위에 올라탈 수 있음(NavMeshObstacle) | 넓은 방 |

- 구현 (2026-09-24, 고양이 49): `World/TrapBase`(호스트 0.1s 폴링 — 박스 안 Active 쥐·풀린 물건) + `MouseTrap`(Downed + 소음 80 NoiseType.Trap, 1회성, 1s 안에 놓이거나 던져진 물건이면 헛격발) · `GluePad`(Trapped → 동료 E 1.5s 구출, 풀려난 쥐 4s 유예) · `WireShock`(켜짐 1.5s / 꺼짐 1.5s 반복, 켜진 동안 Stunned 2s + 소음 30). Roomba는 docs/13 컷 후보 + 집주인 로봇청소기(docs/09)로 대신. 데모 레이아웃에 3종(쥐 구멍 안쪽 쥐덫·끈끈이·창고방 문 앞 전기선). 시드 배치(TrapSpawns·TrapTable)는 존 생성기 태스크.

## 수용 기준 (W8)

- [x] 같은 시드 → 호스트·클라 동일 결과 (클라는 스폰 수신만이므로 자동 보장 — 검증만) (2026-09-24 고양이 58: 에디터 호스트 + macOS 빌드 클라, Stage_Generated. 스폰된 NetworkObject 31/31이 클라에게 보임, 방 4개 전부. 클라 쥐는 쥐구멍방 바닥(y 0.6, 쥐구멍 2.5m)에 섬. 출발 → 적립 12 → 둘 다 쥐구멍 → Returning → Returned → Hub(둘 다 접속). 호스트·클라 예외 0)
- [x] 시드 20개 연속 생성 스트레스 테스트: 오버랩 0, 생성 실패 0, NavMesh 구멍 0 (2026-09-24 고양이 57, `Tools/RatGame/Zone/Stress Test`: 시드 20개 방 4~6(평균 5.1)·보너스 12·겹침 0·실패 0·NavMesh 끊김 0·98ms, 시드 200개도 같음(942ms). 모서리방만으로 강제로 말리게 하면 겹침 후보 5번 버리고 4방에서 멈춤 — 겹침 0)
- [ ] 부엌 테마 그레이박스 방 4종으로 존 3개 연속 플레이
- [x] 반지가 항상 고양이 옆에 있는지, 선반 위 Large 금지 규칙 확인 (2026-09-24 고양이 61: 부엌 맵 시드 3개 — 반지 1개씩, 고양이 침대에서 1.41·1.26·1.09m, 바닥 위. 반지는 무작위 롤에서 빠짐(4000번 중 0), 높은 지점 Large 0/2000)
