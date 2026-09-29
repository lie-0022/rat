using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using RatGame.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 생성기 v2 "벽 속" 스테이지 (docs/10, 2026-09-24 고양이 62). 호스트가 칸 그래프 → 방·통로 스폰(열린 면은 스폰 전에) → NavMesh →
    /// 식량 창고(쥐구멍 존)를 **목적지방**에 → 방 채우기(ZonePopulator) → 쥐를 출발방에.
    /// 창고가 목적지에 있어서 지금 귀환 판정(RunManager — 창고에 전원 집합)이 곧 "목적지 도착으로 끝내기"가 된다 (docs/09 새 루프).
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public partial class GridZoneBuilder : NetworkBehaviour
    {
        [SerializeField] private GridZoneSO _zone;
        [SerializeField] private int _fixedSeed; // 0이면 무작위

        public int Seed { get; private set; }
        public GridPlan Plan { get; private set; }
        public GridZoneLayout.Result Layout { get; private set; }
        public ZonePopulator Populator { get; private set; }
        /// <summary>보물방 칸 번호 (없으면 -1, 고양이 84).</summary>
        public int Treasure { get; private set; } = -1;
        /// <summary>오늘의 집 (고양이 106).</summary>
        public StageModifier Modifier { get; private set; }
        public bool OwnerOut => Modifier == StageModifier.OwnerOut;
        public int ExtraHouseEvents => Modifier == StageModifier.Busy ? _zone.BusyExtraEvents : 0;

        public override void OnNetworkSpawn()
        {
            if (!IsServer || _zone == null) return;
            // 기지에서 출발해 로드 중이면 클라 로딩이 끝난 뒤에 (로드 중 스폰은 클라에 안 간다 — 고양이 59)
            if (RunSession.DepartPending) _loadGate = new SceneLoadGate(NetworkManager, gameObject.scene.name, () => Build(_fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue))); // 유령 연결에 안 묶이게 (고양이 121)
            else Build(_fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue));
        }

        private SceneLoadGate _loadGate;

        public override void OnNetworkDespawn() => _loadGate?.Cancel();

        private void Build(int seed)
        {
            Seed = seed;
            Plan = GridLayoutPlanner.Plan(seed, GridZoneLayout.SettingsOf(_zone));
            var rng = new System.Random(seed ^ 0x6d);
            Layout = GridZoneLayout.Place(_zone, Plan, Vector3.zero, rng, prefab => Instantiate(prefab));
            for (int i = 0; i < Layout.Rooms.Count; i++)
            {
                Layout.Rooms[i].ServerSetOpenSides(Layout.Sides[i]); // 스폰 전에 — 클라는 스폰 때 이 값으로 막음벽을 맞춘다
                Layout.Rooms[i].NetworkObject.Spawn(true);
            }
            foreach (var c in Layout.Corridors) c.GetComponent<NetworkObject>().Spawn(true);

            Physics.SyncTransforms();
            var surface = GetComponent<NavMeshSurface>();
            int catAgent = BigCatAgentType();
            if (catAgent != -1) surface.agentTypeID = catAgent; // 큰 고양이가 벽에 파고들지 않게 그 크기로 굽는다 (고양이 141)
            surface.collectObjects = CollectObjects.All;
            surface.layerMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker");
            surface.BuildNavMesh();

            var pop = _zone.Population;
            var depot = Layout.Destination != null ? Layout.Destination.Depot : null;
            if (pop != null && pop.RatHolePrefab != null && depot != null)
                ZonePopulator.Spawn(pop.RatHolePrefab, depot.position + Vector3.up * 0.15f, depot.rotation);
            // 판매대: 목적지방 모서리 쪽(문은 면 가운데라 막지 않게), 글씨가 창고 쪽을 보게
            if (_zone.ShopCounterPrefab != null && depot != null)
            {
                Vector3 at = depot.position + new Vector3(2.8f, 0f, 2.8f);
                ZonePopulator.Spawn(_zone.ShopCounterPrefab, at, Quaternion.LookRotation(at - depot.position));
            }

            AddPipeAmbushSpots(); // 고양이 스폰(채우기) 전에 — 고양이가 스폰 때 스팟을 모은다
            var rooms = new List<RoomModule>();
            foreach (var r in Layout.Rooms) rooms.Add(r.GetComponent<RoomModule>());
            if (pop != null)
            {
                int stage = RunSession.StageNumber; // 새 루프 깊이 — 고양이 수·함정 비율·고가치 가중 (고양이 66)
                Modifier = PickModifier(seed, stage);
                Treasure = _zone.TreasureRoom ? PickTreasure() : -1;
                Populator = new ZonePopulator(pop, rooms, rooms[0], Treasure >= 0 ? rooms[Treasure] : null)
                {
                    CatCountOverride = _zone.CatCountFor(stage) + (Modifier == StageModifier.Guest ? 1 : 0), // 고양이 손님 (고양이 117)
                    TrapRatioOverride = Mathf.Min(1f, _zone.TrapRatioFor(stage) + (Modifier == StageModifier.TrapSale ? _zone.TrapSaleBonus : 0f)),
                    DarkChanceOverride = Modifier == StageModifier.Blackout ? _zone.BlackoutDarkChance : -1f,
                    BonusTrapRatio = _zone.TreasureTrapRatio,
                    BonusBigWeight = _zone.TreasureBigWeight,
                    CatSizeScale = _zone.CatSizeScale,
                    DarkZoneScale = _zone.RoomScale, // 방이 넓어진 만큼 어둠 구역도 (고양이 147)
                    CatAgentTypeId = catAgent,
                };
                Populator.PopulateAll(rng, stage - 1);
                MaybeBaitTraps(rng);
                if (Treasure >= 0 && _zone.TreasureGlow != null) // 문틈으로 금빛이 새어 보이게 (고양이 88)
                    ZonePopulator.Spawn(_zone.TreasureGlow, Layout.Rooms[Treasure].transform.position, Quaternion.identity);
                MaybeKitten(stage, rng);
                MaybeGuard(stage, rng);
                MaybePatroller(stage, rng);
                if (Modifier == StageModifier.CatTreats)
                    foreach (var cat in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None)) cat.ServerSetStageSpeed(_zone.TreatsCatSpeed);
            }
            FaceSpawnsToDoor(Layout.Start, Layout.Sides[0]);
            DeliverPurchases(Layout.Start, rng);
            PlayerPlacement.TeleportAllToSpawns();
            SendBriefing();
            Log.Dev($"벽 속 스폰: 스테이지 {RunSession.StageNumber}, 시드 {seed}, 오늘 {Modifier} — 방 {rooms.Count}{(Treasure >= 0 ? $" (보물방 {Layout.Rooms[Treasure].transform.position:F0})" : "")}, 통로 {Layout.Corridors.Count}, 고리 {(Plan.HasLoop ? "있음" : "없음")}, " +
                    $"전리품 {Populator?.LootSpawned}개(가치 {Populator?.LootValue}), 함정 {Populator?.TrapsSpawned}, 숨을 곳 {Populator?.HidesSpawned}, 어둠 {Populator?.DarkSpawned}, 고양이 {Populator?.CatsSpawned}");
        }

        // 테마의 큰 고양이 에이전트 타입이 프로젝트 설정에 실제로 있을 때만
        private int BigCatAgentType()
        {
            int id = _zone.CatAgentTypeId;
            return id != -1 && UnityEngine.AI.NavMesh.GetSettingsByID(id).agentTypeID == id ? id : -1; // 번호는 음수일 수 있다 — -1만 "없음"
        }

        public const byte BriefKitten = 1, BriefGuard = 2, BriefPipe = 4, BriefTreasure = 8, BriefPatroller = 16;

        // 스테이지 특징을 전원 화면에 (고양이 81) — 표시일 뿐이라 늦게 들어온 클라는 못 받아도 된다
        private void SendBriefing()
        {
            byte flags = 0;
            foreach (var c in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None))
            {
                if (c.IsKitten) flags |= BriefKitten;
                if (c.IsGuard) flags |= BriefGuard;
                if (c.IsPatroller) flags |= BriefPatroller;
            }
            foreach (var e in Plan.Edges) if (e.IsPipe) { flags |= BriefPipe; break; }
            if (Treasure >= 0) flags |= BriefTreasure;
            var quota = GetComponent<StageQuota>() ?? FindAnyObjectByType<StageQuota>();
            if (quota != null && quota.IsSpawned) quota.Modifier.Value = (byte)Modifier; // 위 막대용 (고양이 120)
            int stage = RunSession.StageNumber;
            StageBriefingClientRpc(stage, quota != null ? quota.StagesTotal : 0, quota != null ? quota.QuotaFor(stage) : 0, flags, (byte)Modifier);
        }

        [ClientRpc]
        private void StageBriefingClientRpc(int stage, int stages, int quota, byte flags, byte modifier)
        {
            Log.Dev($"스테이지 안내 연출: {stage}/{stages} 식량 {quota} 특징 {flags} 오늘 {(StageModifier)modifier}"); // 2인 검증용
            EventBus.RaiseStageBriefing(stage, stages, quota, flags, modifier);
            EventBus.RaiseAchievementStat("deepestStage", stage, true); // 깊은 쥐 — 모든 클라가 각자 (고양이 220)
        }

        /// <summary>호스트: 집주인 덫 놓기 (고양이 89) — 쥐 근처는 피해서. 놓은 수.</summary>
        public int ServerAddTraps(int count, float avoidRadius)
        {
            if (!IsServer || Populator == null) return 0;
            var rats = new List<Vector3>();
            foreach (var c in NetworkManager.ConnectedClientsList) if (c.PlayerObject != null) rats.Add(c.PlayerObject.transform.position);
            int placed = Populator.AddTraps(new System.Random(Random.Range(1, int.MaxValue)), count, rats, avoidRadius);
            Log.Dev($"집주인 덫: {placed}개 놓음 (쥐 {avoidRadius}m 밖)");
            return placed;
        }

        public bool CanAddTraps => Populator != null && Populator.HasFreeTrapSpot();

        // 지난 스테이지 상점에서 산 물건을 출발방 바닥에 (plan cat-65)
        private void DeliverPurchases(GridRoom start, System.Random rng)
        {
            if (RunSession.PendingItems.Count == 0 || _zone.Shop == null || start == null) return;
            var names = new List<string>();
            int i = 0;
            foreach (var id in RunSession.PendingItems)
            {
                if (!_zone.Shop.TryGet(id, out var entry) || entry.Prefab == null) continue;
                float a = i++ * 1.3f;
                Vector3 p = start.transform.position + new Vector3(Mathf.Cos(a) * 1.8f, 0.4f, Mathf.Sin(a) * 1.8f);
                ZonePopulator.Spawn(entry.Prefab, p, Quaternion.identity);
                names.Add(entry.DisplayName);
            }
            RunSession.PendingItems.Clear();
            Log.Dev($"택배: 출발방에 {string.Join(", ", names)}");
        }

        // 출발방은 문이 하나 — 시작하자마자 그 문을 보고 서게 (호스트만 쓰는 자리라 동기화 불필요)
        private static void FaceSpawnsToDoor(GridRoom start, byte sides)
        {
            if (start == null || sides == 0) return;
            int side = 0; while (side < 4 && (sides & (1 << side)) == 0) side++;
            Vector3 door = start.DoorCenter(side);
            foreach (var spawn in start.GetComponent<RoomModule>().PlayerSpawns)
            {
                Vector3 look = door - spawn.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) spawn.rotation = Quaternion.LookRotation(look);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(GridZoneSO zone) => _zone = zone;
#endif
    }
}
