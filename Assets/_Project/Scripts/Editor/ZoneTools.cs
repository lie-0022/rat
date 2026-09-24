using RatGame.AI;
using System.Collections.Generic;
using RatGame.Data;
using RatGame.Run;
using RatGame.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.Editor
{
    /// <summary>
    /// 존 생성기 도구 (docs/10, 2026-09-24 고양이 57): 그레이박스 방 모듈 6종 + 존 정의 에셋 생성, 시드 20개 스트레스 테스트.
    /// 방은 직사각형(W×D) — 남쪽(-Z) 벽 가운데가 Entry, 출구는 (벽, 오프셋)으로. 벽은 문틈 1.8m를 비우고 조각으로 세운다.
    /// </summary>
    public static partial class ZoneTools
    {
        private const string RoomDir = "Assets/_Project/Prefabs/Rooms/Kitchen";
        private const string ZonePath = "Assets/_Project/Data/Zones/Zone_Kitchen_Greybox.asset";
        private const float WallH = 2.5f, WallT = 0.2f, Door = 1.8f;

        private enum Side { North, East, South, West }

        private struct RoomSpec
        {
            public string Name; public float W, D; public bool HasEntry; public (Side side, float offset)[] Exits;
            public bool Cat, RatHole, Bonus; public int Loot, Traps; public CatSpotType[] Spots;
            public Vector2[] Hides; public Vector2? Dark; // 숨을 곳·어둠 구역 자리 (방 로컬 x,z — 고양이 60)
        }

        [MenuItem("Tools/RatGame/Zone/Create Greybox Rooms")]
        public static void CreateGreyboxRooms()
        {
            var specs = new[]
            {
                new RoomSpec { Name = "Room_RatHole",  W = 8,  D = 8,  HasEntry = false, Exits = new[] { (Side.North, 0f) }, RatHole = true, Loot = 3, Traps = 0 },
                new RoomSpec { Name = "Room_Straight", W = 8,  D = 10, HasEntry = true,  Exits = new[] { (Side.North, 0f) }, Cat = true, Loot = 7, Traps = 2, Spots = new[] { CatSpotType.Look, CatSpotType.Food }, Hides = new[] { new Vector2(-2.7f, 3.7f) }, Dark = new Vector2(0f, 2.8f) },
                new RoomSpec { Name = "Room_Corner",   W = 8,  D = 8,  HasEntry = true,  Exits = new[] { (Side.East, 0f) }, Loot = 6, Traps = 2, Spots = new[] { CatSpotType.Look, CatSpotType.Sun }, Hides = new[] { new Vector2(-2.5f, 2.5f) }, Dark = new Vector2(1.9f, -1.9f) },
                new RoomSpec { Name = "Room_Hall",     W = 12, D = 10, HasEntry = true,  Exits = new[] { (Side.North, -3f), (Side.West, 2f) }, Cat = true, Loot = 9, Traps = 2, Spots = new[] { CatSpotType.Bed, CatSpotType.Look, CatSpotType.Groom }, Hides = new[] { new Vector2(4.7f, 3.7f), new Vector2(4.7f, -3.7f) }, Dark = new Vector2(-3.5f, -2.5f) },
                new RoomSpec { Name = "Room_Corridor", W = 4,  D = 12, HasEntry = true,  Exits = new[] { (Side.North, 0f) }, Loot = 4, Traps = 2, Spots = new[] { CatSpotType.Look } },
                new RoomSpec { Name = "Room_Bonus",    W = 8,  D = 8,  HasEntry = true,  Exits = new (Side, float)[0], Bonus = true, Loot = 10, Traps = 1, Spots = new[] { CatSpotType.Bed }, Hides = new[] { new Vector2(-2.5f, 2.5f) } },
            };
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Zones")) AssetDatabase.CreateFolder("Assets/_Project/Data", "Zones");
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var spec in specs) prefabs[spec.Name] = BuildRoom(spec);

            var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinitionSO>(ZonePath);
            if (zone == null) { zone = ScriptableObject.CreateInstance<ZoneDefinitionSO>(); AssetDatabase.CreateAsset(zone, ZonePath); }
            zone.ThemeId = "kitchen";
            zone.RatHoleRoom = prefabs["Room_RatHole"];
            zone.MiddleRoomPool = new[] { prefabs["Room_Straight"], prefabs["Room_Corner"], prefabs["Room_Hall"], prefabs["Room_Corridor"] };
            zone.BonusRoomPool = new[] { prefabs["Room_Bonus"] };
            zone.BonusRoomChance = 0.4f;
            zone.CatCount = 1;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Debug.Log("[RatGame] 그레이박스 방 6종 + Zone_Kitchen_Greybox 생성");
        }

        private static GameObject BuildRoom(RoomSpec s)
        {
            var root = new GameObject(s.Name);
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            var bounds = root.AddComponent<BoxCollider>();
            bounds.isTrigger = true; bounds.center = new Vector3(0f, WallH * 0.5f, 0f); bounds.size = new Vector3(s.W, WallH, s.D);
            var room = root.AddComponent<RoomModule>();
            root.AddComponent<Unity.Netcode.NetworkObject>(); // 호스트가 스폰 — 클라는 지오메트리를 받는다 (고양이 58)
            int wallLayer = LayerMask.NameToLayer("NoiseBlocker");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor"; floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f); floor.transform.localScale = new Vector3(s.W, 0.1f, s.D);
            floor.layer = LayerMask.NameToLayer("RoomStatic"); floor.isStatic = true;
            Tint(floor, s.Bonus ? new Color(0.75f, 0.62f, 0.45f) : s.RatHole ? new Color(0.55f, 0.6f, 0.5f) : new Color(0.7f, 0.68f, 0.62f));

            // 문 위치 모으기: 벽마다 오프셋 목록
            var gaps = new Dictionary<Side, List<float>>();
            foreach (Side side in System.Enum.GetValues(typeof(Side))) gaps[side] = new List<float>();
            if (s.HasEntry) gaps[Side.South].Add(0f);
            foreach (var e in s.Exits) gaps[e.side].Add(e.offset);
            foreach (Side side in System.Enum.GetValues(typeof(Side))) BuildWall(root.transform, s, side, gaps[side], wallLayer);

            if (s.HasEntry) room.Entry = MakeSocket(root.transform, s, Side.South, 0f, "Entry");
            var exits = new List<DoorSocket>();
            for (int i = 0; i < s.Exits.Length; i++) exits.Add(MakeSocket(root.transform, s, s.Exits[i].side, s.Exits[i].offset, "Exit_" + i));
            room.Exits = exits.ToArray();

            // 스폰 표시 — 벽에서 1m 안쪽 격자에 흩뿌림 (시드 무관, 방마다 고정)
            var rng = new System.Random(s.Name.GetHashCode());
            room.LootSpawns = Markers(root.transform, "LootSpawn", s.Loot, s, rng, 1.0f);
            room.TrapSpawns = Markers(root.transform, "TrapSpawn", s.Traps, s, rng, 1.2f);
            if (s.Cat) { var c = new GameObject("CatSpawn").transform; c.SetParent(root.transform, false); c.localPosition = new Vector3(0f, 0f, 0.5f); c.tag = "CatSpawn"; room.CatSpawn = c; }
            if (s.RatHole)
            {
                var h = new GameObject("RatHole").transform; h.SetParent(root.transform, false); h.localPosition = new Vector3(-s.W * 0.5f + 1.5f, 0f, -s.D * 0.5f + 1.5f); h.tag = "RatHole"; room.RatHole = h;
                var ps = new List<Transform>();
                for (int i = 0; i < 4; i++) { var p = new GameObject("PlayerSpawn_" + i).transform; p.SetParent(root.transform, false); p.localPosition = new Vector3(-1.5f + i, 0.65f, -s.D * 0.5f + 3f); p.tag = "PlayerSpawn"; ps.Add(p); }
                room.PlayerSpawns = ps.ToArray();
            }
            // 고양이 스팟 — 벽에서 1.5m 안쪽, 방마다 고정 (docs/10 "방마다 Look ≥1, 존마다 Bed 1")
            if (s.Spots != null)
                for (int i = 0; i < s.Spots.Length; i++)
                {
                    var sp = new GameObject($"Spot_{s.Spots[i]}_{i}").transform; sp.SetParent(root.transform, false);
                    float a = (i + 0.5f) / s.Spots.Length * Mathf.PI * 2f;
                    sp.localPosition = new Vector3(Mathf.Cos(a) * (s.W * 0.5f - 1.5f), 0f, Mathf.Sin(a) * (s.D * 0.5f - 1.5f));
                    sp.localRotation = Quaternion.LookRotation(-sp.localPosition.normalized);
                    sp.gameObject.AddComponent<CatSpot>().EditorSetup(s.Spots[i], 1f);
                }
            // 숨을 곳 자리 — 모서리, 방 가운데를 본다(나오는 쪽). 문틈·가운데 상자는 피해서 방마다 손으로 고름 (고양이 60)
            if (s.Hides != null)
            {
                var hides = new List<Transform>();
                for (int i = 0; i < s.Hides.Length; i++)
                {
                    var h = new GameObject("HideSpawn_" + i).transform; h.SetParent(root.transform, false);
                    h.localPosition = new Vector3(s.Hides[i].x, 0f, s.Hides[i].y);
                    h.localRotation = Quaternion.LookRotation(new Vector3(-s.Hides[i].x, 0f, -s.Hides[i].y).normalized);
                    hides.Add(h);
                }
                room.HideSpawns = hides.ToArray();
            }
            if (s.Dark.HasValue)
            {
                var d = new GameObject("DarkZone").transform; d.SetParent(root.transform, false);
                d.localPosition = new Vector3(s.Dark.Value.x, 0f, s.Dark.Value.y);
                room.DarkZone = d;
            }
            // 가운데 상자 하나 — 시야를 끊는다 (좁은 복도엔 없음)
            if (s.W >= 8)
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "Crate"; crate.transform.SetParent(root.transform, false);
                crate.transform.localPosition = new Vector3(s.W * 0.2f, 0.6f, s.D * 0.15f); crate.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
                crate.layer = LayerMask.NameToLayer("RoomStatic"); crate.isStatic = true;
                Tint(crate, new Color(0.55f, 0.45f, 0.35f));
            }

            string path = $"{RoomDir}/{s.Name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildWall(Transform root, RoomSpec s, Side side, List<float> doors, int layer)
        {
            bool alongX = side == Side.North || side == Side.South;
            float len = alongX ? s.W : s.D;
            float fixedCoord = side switch { Side.North => s.D * 0.5f, Side.South => -s.D * 0.5f, Side.East => s.W * 0.5f, _ => -s.W * 0.5f };
            doors.Sort();
            float cursor = -len * 0.5f;
            var cuts = new List<(float a, float b)>();
            foreach (var d in doors) { cuts.Add((cursor, d - Door * 0.5f)); cursor = d + Door * 0.5f; }
            cuts.Add((cursor, len * 0.5f));
            int n = 0;
            foreach (var (a, b) in cuts)
            {
                if (b - a < 0.05f) continue;
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = $"Wall_{side}_{n++}"; w.transform.SetParent(root, false);
                float mid = (a + b) * 0.5f, size = b - a;
                w.transform.localPosition = alongX ? new Vector3(mid, WallH * 0.5f, fixedCoord) : new Vector3(fixedCoord, WallH * 0.5f, mid);
                w.transform.localScale = alongX ? new Vector3(size, WallH, WallT) : new Vector3(WallT, WallH, size);
                w.layer = layer; w.isStatic = true;
                Tint(w, new Color(0.45f, 0.43f, 0.42f));
            }
        }

        private static DoorSocket MakeSocket(Transform root, RoomSpec s, Side side, float offset, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(root, false); t.tag = "DoorSocket";
            switch (side)
            {
                case Side.North: t.localPosition = new Vector3(offset, 0f, s.D * 0.5f); t.localRotation = Quaternion.LookRotation(Vector3.forward); break;
                case Side.South: t.localPosition = new Vector3(offset, 0f, -s.D * 0.5f); t.localRotation = Quaternion.LookRotation(Vector3.back); break;
                case Side.East: t.localPosition = new Vector3(s.W * 0.5f, 0f, offset); t.localRotation = Quaternion.LookRotation(Vector3.right); break;
                default: t.localPosition = new Vector3(-s.W * 0.5f, 0f, offset); t.localRotation = Quaternion.LookRotation(Vector3.left); break;
            }
            return t.gameObject.AddComponent<DoorSocket>();
        }

        private static Transform[] Markers(Transform root, string tag, int count, RoomSpec s, System.Random rng, float margin)
        {
            var list = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var m = new GameObject($"{tag}_{i}").transform; m.SetParent(root, false);
                float x = (float)(rng.NextDouble() * (s.W - 2 * margin) - (s.W * 0.5f - margin));
                float z = (float)(rng.NextDouble() * (s.D - 2 * margin) - (s.D * 0.5f - margin));
                m.localPosition = new Vector3(x, tag == "LootSpawn" ? 0.3f : 0f, z);
                if (tag == "LootSpawn") m.tag = "LootSpawn";
                list.Add(m);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 생성 스테이지 준비 (고양이 58): Stage_Warehouse01의 쥐구멍·함정 3종을 네트워크 프리팹으로 뽑고, 전리품·함정 테이블을 만들고,
        /// 존 정의에 연결한 뒤 Stage_Generated 씬(빛·카메라·RunManager + ZoneBuilder만)을 만들어 빌드 목록에 넣는다.
        /// Stage_Warehouse01을 연 상태에서 실행.
        /// </summary>
        [MenuItem("Tools/RatGame/Zone/Create Generated Stage")]
        public static void CreateGeneratedStage()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Stage_Warehouse01") { Debug.LogWarning("[RatGame] Stage_Warehouse01을 연 뒤 실행"); return; }
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinitionSO>(ZonePath);

            GameObject ToPrefab(string sceneName, string path)
            {
                var src = GameObject.Find(sceneName);
                if (src == null) { Debug.LogWarning($"[RatGame] {sceneName} 없음"); return AssetDatabase.LoadAssetAtPath<GameObject>(path); }
                var copy = Object.Instantiate(src); copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
                PersistMaterials(copy);
                copy.transform.SetParent(null); copy.transform.position = new Vector3(0f, src.transform.position.y, 0f); copy.transform.rotation = Quaternion.identity;
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
                Object.DestroyImmediate(copy);
                return prefab;
            }
            var ratHole = ToPrefab("RatHole", "Assets/_Project/Prefabs/Net/RatHoleZone.prefab");
            var mouse = ToPrefab("Trap_MouseTrap", "Assets/_Project/Prefabs/Traps/Trap_MouseTrap.prefab");
            var glue = ToPrefab("Trap_GluePad", "Assets/_Project/Prefabs/Traps/Trap_GluePad.prefab");
            var wire = ToPrefab("Trap_WireShock", "Assets/_Project/Prefabs/Traps/Trap_WireShock.prefab");

            // 전리품 테이블 — 티어별 가중치 (헤어볼은 고양이가 뱉는 것, 다운 몸은 전리품 아님)
            var loot = AssetDatabase.LoadAssetAtPath<SpawnTableSO>("Assets/_Project/Data/Zones/LootTable_Kitchen.asset");
            if (loot == null) { loot = ScriptableObject.CreateInstance<SpawnTableSO>(); AssetDatabase.CreateAsset(loot, "Assets/_Project/Data/Zones/LootTable_Kitchen.asset"); }
            var entries = new List<SpawnEntry>();
            foreach (var g in AssetDatabase.FindAssets("t:LootItemSO", new[] { "Assets/_Project/Data/Items" }))
            {
                var so = AssetDatabase.LoadAssetAtPath<LootItemSO>(AssetDatabase.GUIDToAssetPath(g));
                if (so == null || so.Id == null || !so.Id.StartsWith("loot_") || so.Id == "loot_hairball" || so.Prefab == null) continue;
                int w = so.Tier switch { LootTier.Small => 10, LootTier.Tricky => 6, LootTier.Large => 3, _ => 1 };
                int max = so.Tier switch { LootTier.Large => 3, LootTier.Special => 1, _ => 0 };
                entries.Add(new SpawnEntry { Item = so, Weight = w, Max = max });
            }
            loot.Entries = entries.ToArray(); loot.UseRatio = 0.7f; EditorUtility.SetDirty(loot);

            var traps = AssetDatabase.LoadAssetAtPath<TrapTableSO>("Assets/_Project/Data/Zones/TrapTable_Kitchen.asset");
            if (traps == null) { traps = ScriptableObject.CreateInstance<TrapTableSO>(); AssetDatabase.CreateAsset(traps, "Assets/_Project/Data/Zones/TrapTable_Kitchen.asset"); }
            traps.Entries = new[] { new TrapEntry { Prefab = mouse, Weight = 3 }, new TrapEntry { Prefab = glue, Weight = 2 }, new TrapEntry { Prefab = wire, Weight = 1 } };
            traps.UseRatio = 0.5f; EditorUtility.SetDirty(traps);

            zone.LootTable = loot; zone.TrapTable = traps; zone.RatHolePrefab = ratHole;
            zone.CatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Cat/Cat.prefab");
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();

            // 씬: 창고 씬을 복사해 빛·카메라·RunManager만 남기고 ZoneBuilder 붙이기
            const string genPath = "Assets/_Project/Scenes/Stage_Generated.unity";
            string warehousePath = scene.path;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(genPath) == null) AssetDatabase.CopyAsset(warehousePath, genPath);
            var gen = EditorSceneManager.OpenScene(genPath, OpenSceneMode.Single);
            foreach (var root in gen.GetRootGameObjects())
                if (root.name != "Directional Light" && root.name != "Main Camera" && root.name != "RunManager") Object.DestroyImmediate(root);
            var rm = GameObject.Find("RunManager");
            if (rm.GetComponent<NavMeshSurface>() == null) rm.AddComponent<NavMeshSurface>();
            var builder = rm.GetComponent<ZoneBuilder>() ?? rm.AddComponent<ZoneBuilder>();
            builder.EditorSetup(zone, balance);
            EditorSceneManager.MarkSceneDirty(gen);
            EditorSceneManager.SaveScene(gen);

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(x => x.path == genPath)) { list.Add(new EditorBuildSettingsScene(genPath, true)); EditorBuildSettings.scenes = list.ToArray(); }
            EditorSceneManager.OpenScene(warehousePath, OpenSceneMode.Single);
            Debug.Log($"[RatGame] 생성 스테이지 준비 — 전리품 {entries.Count}종, 함정 3종, {genPath}");
        }
    }
}
