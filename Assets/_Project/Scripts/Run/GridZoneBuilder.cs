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
    public class GridZoneBuilder : NetworkBehaviour
    {
        [SerializeField] private GridZoneSO _zone;
        [SerializeField] private int _fixedSeed; // 0이면 무작위

        public int Seed { get; private set; }
        public GridPlan Plan { get; private set; }
        public GridZoneLayout.Result Layout { get; private set; }
        public ZonePopulator Populator { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsServer || _zone == null) return;
            // 기지에서 출발해 로드 중이면 클라 로딩이 끝난 뒤에 (로드 중 스폰은 클라에 안 간다 — 고양이 59)
            if (RunSession.DepartPending) NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            else Build(_fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue));
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }

        private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != gameObject.scene.name) return;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            Build(_fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue));
        }

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

            var rooms = new List<RoomModule>();
            foreach (var r in Layout.Rooms) rooms.Add(r.GetComponent<RoomModule>());
            if (pop != null)
            {
                int stage = RunSession.StageNumber; // 새 루프 깊이 — 고양이 수·함정 비율·고가치 가중 (고양이 66)
                Populator = new ZonePopulator(pop, rooms, rooms[0], null)
                {
                    CatCountOverride = _zone.CatCountFor(stage),
                    TrapRatioOverride = _zone.TrapRatioFor(stage),
                };
                Populator.PopulateAll(rng, stage - 1);
                MaybeKitten(stage, rng);
                MaybeGuard(stage, rng);
            }
            FaceSpawnsToDoor(Layout.Start, Layout.Sides[0]);
            DeliverPurchases(Layout.Start, rng);
            PlayerPlacement.TeleportAllToSpawns();
            SendBriefing();
            Log.Dev($"벽 속 스폰: 스테이지 {RunSession.StageNumber}, 시드 {seed} — 방 {rooms.Count}, 통로 {Layout.Corridors.Count}, 고리 {(Plan.HasLoop ? "있음" : "없음")}, " +
                    $"전리품 {Populator?.LootSpawned}개(가치 {Populator?.LootValue}), 함정 {Populator?.TrapsSpawned}, 숨을 곳 {Populator?.HidesSpawned}, 어둠 {Populator?.DarkSpawned}, 고양이 {Populator?.CatsSpawned}");
        }

        public const byte BriefKitten = 1, BriefGuard = 2, BriefPipe = 4;

        // 스테이지 특징을 전원 화면에 (고양이 81) — 표시일 뿐이라 늦게 들어온 클라는 못 받아도 된다
        private void SendBriefing()
        {
            byte flags = 0;
            foreach (var c in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None))
            {
                if (c.IsKitten) flags |= BriefKitten;
                if (c.IsGuard) flags |= BriefGuard;
            }
            foreach (var e in Plan.Edges) if (e.IsPipe) { flags |= BriefPipe; break; }
            var quota = GetComponent<StageQuota>() ?? FindAnyObjectByType<StageQuota>();
            int stage = RunSession.StageNumber;
            StageBriefingClientRpc(stage, quota != null ? quota.StagesTotal : 0, quota != null ? quota.QuotaFor(stage) : 0, flags);
        }

        [ClientRpc]
        private void StageBriefingClientRpc(int stage, int stages, int quota, byte flags)
        {
            Log.Dev($"스테이지 안내 연출: {stage}/{stages} 식량 {quota} 특징 {flags}"); // 2인 검증용
            EventBus.RaiseStageBriefing(stage, stages, quota, flags);
        }

        // 깊은 스테이지에서 2마리 이상이면 확률로 한 마리를 아기로 — 엄마·아기 관계(부르면 엄마가 달려옴)는 CatRelation이 붙인다 (고양이 75)
        private void MaybeKitten(int stage, System.Random rng)
        {
            var cats = FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None);
            if (cats.Length < 2 || rng.NextDouble() >= _zone.KittenChanceFor(stage)) return;
            var kitten = cats[rng.Next(cats.Length)];
            if (kitten.ServerMakeKitten()) Log.Dev($"벽 속: 스테이지 {stage} — {kitten.name}를 아기로 (엄마·아기)");
        }

        // 깊은 스테이지: 어른 한 마리를 목적지 앞 문지기로 — 목적지 문(배관 아닌 면) 너머 이웃 방 문 안쪽에 초소 (고양이 80)
        private void MaybeGuard(int stage, System.Random rng)
        {
            var dest = Layout.Destination;
            var cats = FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None);
            var adults = new List<AI.CatBrain>();
            foreach (var c in cats) if (!c.IsKitten) adults.Add(c);
            if (dest == null || cats.Length < 2 || adults.Count == 0 || rng.NextDouble() >= _zone.GuardChanceFor(stage)) return;
            int di = Plan.DestinationIndex;
            foreach (var e in Plan.Edges)
            {
                if (e.IsPipe || (e.A != di && e.B != di)) continue;
                int other = e.A == di ? e.B : e.A;
                int side = GridZoneLayout.SideIndex(Plan.Cells[other].Pos - Plan.Cells[di].Pos);
                var neighbor = Layout.Rooms[other];
                Vector3 door = neighbor.DoorCenter((side + 2) % 4);
                Vector3 inward = neighbor.transform.position - door; inward.y = 0f; inward.Normalize();
                var post = new GameObject("GuardPost").transform;
                post.SetPositionAndRotation(door + inward * 1.5f, Quaternion.LookRotation(inward)); // 들어오는 쥐 쪽을 본다
                post.SetParent(neighbor.transform, true);
                post.gameObject.AddComponent<AI.CatSpot>();
                var guard = adults[rng.Next(adults.Count)];
                guard.ServerMakeGuard(post.position);
                Log.Dev($"벽 속: 스테이지 {stage} — {guard.name}가 목적지 앞 문지기");
                return;
            }
        }

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
