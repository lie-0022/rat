using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using RatGame.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.Run
{
    /// <summary>
    /// 생성 스테이지 (docs/10, 2026-09-24 고양이 58). 호스트가 스테이지 씬이 스폰될 때 존을 만들고 전부 NetworkObject로 스폰 —
    /// 클라는 스폰을 받기만 한다(같은 시드 보장 불필요). 순서: 방 → NavMesh 굽기 → 쥐구멍 → 전리품(테이블 70%) → 함정(50%) → 고양이 → 쥐 배치.
    /// 방 프리팹은 정적 지오메트리만(중첩 NetworkObject 없음), 움직이는 건 전부 따로 스폰.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class ZoneBuilder : NetworkBehaviour
    {
        [SerializeField] private ZoneDefinitionSO _zone;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private int _fixedSeed; // 0이면 무작위 (테스트용 고정)

        public ZoneLayout Layout { get; private set; }
        public int Seed { get; private set; }
        public int LootSpawned { get; private set; }
        public int LootValue { get; private set; }
        public int TrapsSpawned { get; private set; }
        public int CatsSpawned { get; private set; }
        public int HidesSpawned { get; private set; }
        public int DarkSpawned { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsServer || _zone == null) return;
            // 기지에서 출발해 씬을 로드하는 중이면 클라가 아직 로딩 중 — 이때 스폰한 오브젝트는 클라에 안 간다
            // (NGO가 버림: 방·전리품 없이 허공에 섬, 고양이 59). 전원 로드 완료 뒤에 만든다. 씬 직접 플레이는 바로.
            if (RunSession.DepartPending) NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            else BuildNow();
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
            BuildNow();
        }

        private void BuildNow() => Build(_fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue), 0);

        private void Build(int seed, int zoneIndex)
        {
            Seed = seed;
            var rng = new System.Random(seed ^ 0x5eed);
            Layout = new ZoneGenerator().Generate(_zone, _balance, seed, zoneIndex, null);
            var rooms = new List<RoomModule>(Layout.Rooms);
            if (Layout.BonusRoom != null) rooms.Add(Layout.BonusRoom);
            foreach (var r in rooms) r.GetComponent<NetworkObject>().Spawn(true);

            // NavMesh — 방 지오메트리만 (바닥·벽·상자 레이어). 움직이는 물건·쥐·고양이는 넣지 않는다
            Physics.SyncTransforms();
            var surface = GetComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.layerMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker");
            surface.BuildNavMesh();

            var first = Layout.Rooms[0];
            if (_zone.RatHolePrefab != null && first.RatHole != null)
                Spawn(_zone.RatHolePrefab, first.RatHole.position + Vector3.up * 0.15f, first.RatHole.rotation);

            SpawnLoot(rooms, rng, zoneIndex);
            SpawnTraps(rooms, rng);
            SpawnHidesAndDark(rooms, rng); // 고양이보다 먼저 — 고양이가 스폰 때 상자 입구 스팟을 모은다
            SpawnCats(rooms, rng);
            PlayerPlacement.TeleportAllToSpawns(); // 직접 플레이(기지를 안 거침)에서도 쥐구멍방에 서게
            Log.Dev($"존 스폰: 시드 {seed} — 방 {rooms.Count}, 전리품 {LootSpawned}개(가치 {LootValue}), 함정 {TrapsSpawned}, 숨을 곳 {HidesSpawned}, 어둠 {DarkSpawned}, 고양이 {CatsSpawned}");
        }

        private void SpawnLoot(List<RoomModule> rooms, System.Random rng, int zoneIndex)
        {
            var table = _zone.LootTable;
            if (table == null || table.Entries == null) return;
            var counts = new Dictionary<LootItemSO, int>();
            foreach (var room in rooms)
            {
                bool bonus = room == Layout.BonusRoom;
                var points = new List<Transform>(room.LootSpawns);
                Shuffle(points, rng);
                int use = Mathf.CeilToInt(points.Count * (bonus ? 1f : table.UseRatio));
                for (int i = 0; i < use; i++)
                {
                    var p = points[i];
                    bool high = p.position.y > 1f; // 선반 위엔 대형 금지 (docs/10)
                    var item = PickLoot(table, rng, zoneIndex, bonus, high, counts);
                    if (item == null || item.Prefab == null) continue;
                    counts[item] = (counts.TryGetValue(item, out int c) ? c : 0) + 1;
                    Spawn(item.Prefab, p.position + Vector3.up * (item.Tier == LootTier.Large ? 0.4f : 0.1f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    LootSpawned++;
                    LootValue += item.BaseValue;
                }
            }
        }

        private static LootItemSO PickLoot(SpawnTableSO table, System.Random rng, int zoneIndex, bool bonus, bool high, Dictionary<LootItemSO, int> counts)
        {
            float total = 0f;
            var weights = new float[table.Entries.Length];
            for (int i = 0; i < table.Entries.Length; i++)
            {
                var e = table.Entries[i];
                if (e.Item == null || e.Weight <= 0) continue;
                if (e.Max > 0 && counts.TryGetValue(e.Item, out int c) && c >= e.Max) continue;
                bool big = e.Item.Tier == LootTier.Large || e.Item.Tier == LootTier.Special;
                if (high && e.Item.Tier == LootTier.Large) continue;
                float w = e.Weight;
                if (big) w *= (1f + table.DeepZoneBonusPerIndex * zoneIndex) * (bonus ? 3f : 1f); // 보너스방 = 고가치
                weights[i] = w; total += w;
            }
            if (total <= 0f) return null;
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++) { roll -= weights[i]; if (roll <= 0f && weights[i] > 0f) return table.Entries[i].Item; }
            return null;
        }

        private void SpawnTraps(List<RoomModule> rooms, System.Random rng)
        {
            var table = _zone.TrapTable;
            if (table == null || table.Entries == null || table.Entries.Length == 0) return;
            int total = 0; foreach (var e in table.Entries) total += Mathf.Max(0, e.Weight);
            foreach (var room in rooms)
            {
                if (room == Layout.Rooms[0]) continue; // 쥐구멍방엔 함정 없음 — 시작하자마자 걸리지 않게
                foreach (var p in room.TrapSpawns)
                {
                    if (rng.NextDouble() >= table.UseRatio || total <= 0) continue;
                    int roll = rng.Next(total); GameObject prefab = null;
                    foreach (var e in table.Entries) { roll -= e.Weight; if (roll < 0) { prefab = e.Prefab; break; } }
                    if (prefab == null) continue;
                    Spawn(prefab, p.position + Vector3.up * prefab.transform.position.y, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
                    TrapsSpawned++;
                }
            }
        }

        private void SpawnHidesAndDark(List<RoomModule> rooms, System.Random rng)
        {
            int hides = 0, dark = 0;
            foreach (var room in rooms)
            {
                if (_zone.HideSpotPrefabs != null && _zone.HideSpotPrefabs.Length > 0 && room.HideSpawns != null)
                    foreach (var p in room.HideSpawns)
                    {
                        var prefab = _zone.HideSpotPrefabs[rng.Next(_zone.HideSpotPrefabs.Length)];
                        if (prefab == null) continue;
                        Spawn(prefab, p.position + Vector3.up * prefab.transform.position.y, p.rotation);
                        hides++;
                    }
                if (_zone.DarkZonePrefab != null && room.DarkZone != null && rng.NextDouble() < _zone.DarkZoneChance)
                {
                    Spawn(_zone.DarkZonePrefab, room.DarkZone.position, room.DarkZone.rotation);
                    dark++;
                }
            }
            HidesSpawned = hides; DarkSpawned = dark;
        }

        private void SpawnCats(List<RoomModule> rooms, System.Random rng)
        {
            if (_zone.CatPrefab == null) return;
            var candidates = new List<RoomModule>();
            foreach (var r in rooms) if (r.CatSpawn != null) candidates.Add(r);
            Shuffle(candidates, rng);
            int n = Mathf.Min(_zone.CatCount, candidates.Count);
            for (int i = 0; i < n; i++)
            {
                Vector3 at = candidates[i].CatSpawn.position;
                if (NavMesh.SamplePosition(at, out var hit, 2f, NavMesh.AllAreas)) at = hit.position;
                Spawn(_zone.CatPrefab, at, candidates[i].CatSpawn.rotation);
                CatsSpawned++;
            }
        }

        private static void Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = Instantiate(prefab, pos, rot);
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }

#if UNITY_EDITOR
        public void EditorSetup(ZoneDefinitionSO zone, BalanceConfigSO balance) { _zone = zone; _balance = balance; }
#endif
    }
}
