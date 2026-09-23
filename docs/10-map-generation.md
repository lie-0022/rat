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
    public Transform CatSpawn;          // 없으면 이 방엔 고양이 배정 안 함
    public Transform[] TrapSpawns;      // 2~4개
    public LightZone[] DarkZones;       // 어두운 영역 (07 시야 감쇠)
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

## 수용 기준 (W8)

- [ ] 같은 시드 → 호스트·클라 동일 결과 (클라는 스폰 수신만이므로 자동 보장 — 검증만)
- [ ] 시드 20개 연속 생성 스트레스 테스트: 오버랩 0, 생성 실패 0, NavMesh 구멍 0
- [ ] 부엌 테마 그레이박스 방 4종으로 존 3개 연속 플레이
- [ ] 반지가 항상 고양이 옆에 있는지, 선반 위 Large 금지 규칙 확인
