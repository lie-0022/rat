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
                Populator = new ZonePopulator(pop, rooms, rooms[0], null);
                Populator.PopulateAll(rng, 0);
            }
            FaceSpawnsToDoor(Layout.Start, Layout.Sides[0]);
            DeliverPurchases(Layout.Start, rng);
            PlayerPlacement.TeleportAllToSpawns();
            Log.Dev($"벽 속 스폰: 시드 {seed} — 방 {rooms.Count}, 통로 {Layout.Corridors.Count}, 고리 {(Plan.HasLoop ? "있음" : "없음")}, " +
                    $"전리품 {Populator?.LootSpawned}개(가치 {Populator?.LootValue}), 함정 {Populator?.TrapsSpawned}, 숨을 곳 {Populator?.HidesSpawned}, 어둠 {Populator?.DarkSpawned}, 고양이 {Populator?.CatsSpawned}");
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
