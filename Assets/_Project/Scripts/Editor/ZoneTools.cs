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
    public static class ZoneTools
    {
        private const string RoomDir = "Assets/_Project/Prefabs/Rooms/Kitchen";
        private const string ZonePath = "Assets/_Project/Data/Zones/Zone_Kitchen_Greybox.asset";
        private const float WallH = 2.5f, WallT = 0.2f, Door = 1.8f;

        private enum Side { North, East, South, West }

        private struct RoomSpec
        {
            public string Name; public float W, D; public bool HasEntry; public (Side side, float offset)[] Exits;
            public bool Cat, RatHole, Bonus; public int Loot, Traps;
        }

        [MenuItem("Tools/RatGame/Zone/Create Greybox Rooms")]
        public static void CreateGreyboxRooms()
        {
            var specs = new[]
            {
                new RoomSpec { Name = "Room_RatHole",  W = 8,  D = 8,  HasEntry = false, Exits = new[] { (Side.North, 0f) }, RatHole = true, Loot = 3, Traps = 0 },
                new RoomSpec { Name = "Room_Straight", W = 8,  D = 10, HasEntry = true,  Exits = new[] { (Side.North, 0f) }, Cat = true, Loot = 7, Traps = 2 },
                new RoomSpec { Name = "Room_Corner",   W = 8,  D = 8,  HasEntry = true,  Exits = new[] { (Side.East, 0f) }, Loot = 6, Traps = 2 },
                new RoomSpec { Name = "Room_Hall",     W = 12, D = 10, HasEntry = true,  Exits = new[] { (Side.North, -3f), (Side.West, 2f) }, Cat = true, Loot = 9, Traps = 2 },
                new RoomSpec { Name = "Room_Corridor", W = 4,  D = 12, HasEntry = true,  Exits = new[] { (Side.North, 0f) }, Loot = 4, Traps = 2 },
                new RoomSpec { Name = "Room_Bonus",    W = 8,  D = 8,  HasEntry = true,  Exits = new (Side, float)[0], Bonus = true, Loot = 10, Traps = 1 },
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

        private static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(r.sharedMaterial) { color = c };
        }

        [MenuItem("Tools/RatGame/Zone/Stress Test (20 seeds)")]
        public static void StressTest() => RunStress(20, 1000);

        /// <summary>시드 n개: 겹침 쌍·방 수·보너스·NavMesh 도달(쥐구멍방 → 모든 방 중심)·시간. 임시 씬에서, 끝나면 닫는다.</summary>
        public static string RunStress(int count, int firstSeed)
        {
            var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinitionSO>(ZonePath);
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var prevActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);
            int overlaps = 0, failures = 0, navHoles = 0, bonus = 0, totalRooms = 0, minRooms = 99, maxRooms = 0, rejects = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var notes = new List<string>();
            for (int i = 0; i < count; i++)
            {
                int seed = firstSeed + i;
                var parent = new GameObject("Zone_" + seed).transform;
                parent.position = new Vector3(1000f, 0f, 1000f); // 스테이지 NavMesh와 안 섞이게 멀리
                var gen = new ZoneGenerator();
                ZoneLayout layout;
                try { layout = gen.Generate(zone, balance, seed, 0, parent); }
                catch (System.Exception e) { failures++; notes.Add($"시드 {seed} 예외 {e.Message}"); Object.DestroyImmediate(parent.gameObject); continue; }
                var all = new List<RoomModule>(layout.Rooms); if (layout.BonusRoom != null) { all.Add(layout.BonusRoom); bonus++; }
                totalRooms += all.Count; minRooms = Mathf.Min(minRooms, all.Count); maxRooms = Mathf.Max(maxRooms, all.Count); rejects += layout.OverlapRejects;
                if (layout.Rooms.Count < 2) failures++;
                Physics.SyncTransforms();
                for (int a = 0; a < all.Count; a++)
                    for (int b = a + 1; b < all.Count; b++)
                    {
                        var A = all[a].WorldBounds; var B = all[b].WorldBounds;
                        A.Expand(-0.1f); B.Expand(-0.1f);
                        if (A.Intersects(B)) { overlaps++; notes.Add($"시드 {seed} 겹침 {all[a].name}-{all[b].name}"); }
                    }
                // NavMesh: 이 존만 굽고 쥐구멍방 → 각 방 중심
                var surface = parent.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.BuildNavMesh();
                Vector3 start = all[0].transform.position;
                foreach (var r in all)
                {
                    var path = new NavMeshPath();
                    Vector3 target = r.transform.position;
                    NavMesh.SamplePosition(start, out var hs, 2f, NavMesh.AllAreas);
                    NavMesh.SamplePosition(target, out var ht, 3f, NavMesh.AllAreas);
                    NavMesh.CalculatePath(hs.position, ht.position, NavMesh.AllAreas, path);
                    if (path.status != NavMeshPathStatus.PathComplete) { navHoles++; notes.Add($"시드 {seed} {r.name} 경로 {path.status}"); }
                }
                surface.RemoveData();
                Object.DestroyImmediate(parent.gameObject);
            }
            sw.Stop();
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(temp, true);
            string result = $"시드 {count}개: 방 평균 {(float)totalRooms / count:0.0} (최소 {minRooms} 최대 {maxRooms}), 보너스 {bonus}, 겹침 버림 {rejects}, 겹침 쌍 {overlaps}, 생성 실패 {failures}, NavMesh 끊김 {navHoles}, {sw.ElapsedMilliseconds}ms" +
                            (notes.Count > 0 ? " | " + string.Join(" ; ", notes.GetRange(0, Mathf.Min(8, notes.Count))) : "");
            Debug.Log("[ZONESTRESS] " + result);
            return result;
        }
    }
}
