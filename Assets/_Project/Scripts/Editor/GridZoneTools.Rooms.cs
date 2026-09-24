using System.Collections.Generic;
using RatGame.AI;
using RatGame.Data;
using RatGame.World;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// "벽 속" 방 킷 (docs/10 생성기 v2, 고양이 62): 네 면 가운데 문틈(1.8m)+막음벽, 크기별 방 7종, 통로·파이프, 테마 에셋.
    /// 방은 RoomModule(스폰 표시) + GridRoom(막음벽·열린 면) + NetworkObject. 다시 실행하면 덮어쓴다.
    /// </summary>
    public static partial class GridZoneTools
    {
        private const string RoomDir = "Assets/_Project/Prefabs/Rooms/Walls";
        private const string ZonePath = "Assets/_Project/Data/Zones/Zone_Walls.asset";
        private const float WallH = 2.5f, WallT = 0.2f, Door = 1.8f;

        // 벽 속 색 — 나무 바닥·회반죽 벽·단열재 분홍 포인트
        private static readonly Color FloorColor = new(0.42f, 0.32f, 0.24f);
        private static readonly Color WallColor = new(0.78f, 0.73f, 0.64f);
        private static readonly Color PlugColor = new(0.70f, 0.64f, 0.55f);
        private static readonly Color CrateColor = new(0.55f, 0.45f, 0.35f);
        private static readonly Color StartFloor = new(0.40f, 0.45f, 0.35f);
        private static readonly Color DestFloor = new(0.75f, 0.60f, 0.35f);
        private static readonly Color PipeColor = new(0.55f, 0.58f, 0.62f);

        private enum Kind { Normal, Start, Destination } // Normal이 기본값이어야 한다 (종류를 안 적은 방)

        private struct Spec
        {
            public string Name; public float W, D; public Kind Kind;
            public CatSpotType[] Spots; public int Hides; public bool Dark, Cat;
        }

        [MenuItem("Tools/RatGame/Zone/Create Wall Rooms")]
        public static void CreateWallRooms()
        {
            if (!AssetDatabase.IsValidFolder(RoomDir)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs/Rooms", "Walls");
            var specs = new[]
            {
                new Spec { Name = "Wall_Start",  W = 8,  D = 8,  Kind = Kind.Start },
                new Spec { Name = "Wall_Dest",   W = 10, D = 10, Kind = Kind.Destination },
                new Spec { Name = "Wall_Small",  W = 6,  D = 6,  Spots = new[] { CatSpotType.Look, CatSpotType.Door }, Hides = 1 }, // Door = 집 쪽 구멍 — 집주인이 부르면 여기로 나갔다 들어옴
                new Spec { Name = "Wall_Medium", W = 8,  D = 8,  Spots = new[] { CatSpotType.Look, CatSpotType.Bed }, Hides = 1, Dark = true, Cat = true },
                new Spec { Name = "Wall_Wide",   W = 12, D = 7,  Spots = new[] { CatSpotType.Look, CatSpotType.Food, CatSpotType.Sun }, Hides = 1, Dark = true, Cat = true },
                new Spec { Name = "Wall_Tall",   W = 7,  D = 12, Spots = new[] { CatSpotType.Look, CatSpotType.Groom }, Hides = 1, Dark = true, Cat = true },
                new Spec { Name = "Wall_Big",    W = 12, D = 12, Spots = new[] { CatSpotType.Look, CatSpotType.Bed, CatSpotType.Food, CatSpotType.Door }, Hides = 2, Dark = true, Cat = true },
            };
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var s in specs) prefabs[s.Name] = BuildRoom(s);

            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            if (zone == null) { zone = ScriptableObject.CreateInstance<GridZoneSO>(); AssetDatabase.CreateAsset(zone, ZonePath); }
            zone.StartRoom = prefabs["Wall_Start"];
            zone.DestinationRoom = prefabs["Wall_Dest"];
            zone.MainRoomPool = new[] { prefabs["Wall_Medium"], prefabs["Wall_Wide"], prefabs["Wall_Tall"], prefabs["Wall_Big"] };
            zone.BranchRoomPool = new[] { prefabs["Wall_Small"], prefabs["Wall_Medium"] };
            zone.DeadEndRoomPool = new[] { prefabs["Wall_Small"], prefabs["Wall_Medium"], prefabs["Wall_Wide"] };
            zone.Corridor = BuildCorridor("Wall_Corridor", WallColor);
            zone.Pipe = BuildCorridor("Wall_Pipe", PipeColor);
            zone.Population = AssetDatabase.LoadAssetAtPath<ZoneDefinitionSO>("Assets/_Project/Data/Zones/Zone_Kitchen_Greybox.asset");
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RatGame] 벽 속 방 킷 — 방 {specs.Length}종 + 통로·파이프 → {ZonePath}");
        }

        private static GameObject BuildRoom(Spec s)
        {
            var root = new GameObject(s.Name) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            var bounds = root.AddComponent<BoxCollider>();
            bounds.isTrigger = true; bounds.center = new Vector3(0f, WallH * 0.5f, 0f); bounds.size = new Vector3(s.W, WallH, s.D);
            var room = root.AddComponent<RoomModule>();
            root.AddComponent<Unity.Netcode.NetworkObject>();
            var grid = root.AddComponent<GridRoom>();
            int wallLayer = LayerMask.NameToLayer("NoiseBlocker"), floorLayer = LayerMask.NameToLayer("RoomStatic");
            var floorColor = s.Kind == Kind.Start ? StartFloor : s.Kind == Kind.Destination ? DestFloor : FloorColor;
            Box(root.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(s.W, 0.1f, s.D), floorLayer, floorColor);

            // 네 면: 문틈을 비운 벽 두 조각 + 문틈 막음벽 (N, E, S, W 순서 = GridRoom 비트 순서)
            var plugs = new GameObject[4];
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side == 0 || side == 2;
                float len = alongX ? s.W : s.D;
                float fixedCoord = side switch { 0 => s.D * 0.5f, 1 => s.W * 0.5f, 2 => -s.D * 0.5f, _ => -s.W * 0.5f };
                float seg = (len - Door) * 0.5f;
                for (int k = -1; k <= 1; k += 2)
                {
                    float c = k * (Door * 0.5f + seg * 0.5f);
                    Vector3 pos = alongX ? new Vector3(c, WallH * 0.5f, fixedCoord) : new Vector3(fixedCoord, WallH * 0.5f, c);
                    Vector3 size = alongX ? new Vector3(seg, WallH, WallT) : new Vector3(WallT, WallH, seg);
                    Box(root.transform, $"Wall_{side}_{(k < 0 ? "a" : "b")}", pos, size, wallLayer, WallColor);
                }
                Vector3 plugPos = alongX ? new Vector3(0f, WallH * 0.5f, fixedCoord) : new Vector3(fixedCoord, WallH * 0.5f, 0f);
                Vector3 plugSize = alongX ? new Vector3(Door, WallH, WallT) : new Vector3(WallT, WallH, Door);
                plugs[side] = Box(root.transform, $"Plug_{"NESW"[side]}", plugPos, plugSize, wallLayer, PlugColor);
            }

            Transform depot = null;
            var rng = new System.Random(s.Name.GetHashCode());
            if (s.Kind == Kind.Start)
            {
                var ps = new List<Transform>();
                for (int i = 0; i < 4; i++) ps.Add(Marker(root.transform, "PlayerSpawn_" + i, new Vector3(-1.5f + i, 0.65f, 0f), "PlayerSpawn"));
                room.PlayerSpawns = ps.ToArray();
                room.LootSpawns = Scatter(root.transform, "LootSpawn", 2, s, rng);
            }
            else if (s.Kind == Kind.Destination)
            {
                depot = Marker(root.transform, "Depot", Vector3.zero, null); // 식량 창고·상점 자리 (새 루프 2단계)
                room.LootSpawns = new Transform[0];
                // 안전지대 (고양이 72): 고양이 시야·청각 제외 + 고양이 NavMesh 제외 + 따뜻한 불빛(멀리서도 보이게)
                var safe = new GameObject("SafeZone");
                safe.layer = LayerMask.NameToLayer("Ignore Raycast");
                safe.transform.SetParent(root.transform, false);
                var safeBox = safe.AddComponent<BoxCollider>(); safeBox.isTrigger = true;
                safeBox.center = new Vector3(0f, WallH * 0.5f, 0f); safeBox.size = new Vector3(s.W - 0.4f, WallH, s.D - 0.4f);
                safe.AddComponent<SafeZone>();
                var noCat = new GameObject("NoCatVolume") { layer = floorLayer };
                noCat.transform.SetParent(root.transform, false);
                var vol = noCat.AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
                vol.center = new Vector3(0f, WallH * 0.5f, 0f); vol.size = new Vector3(s.W, WallH + 1f, s.D);
                vol.area = 1; // Not Walkable
                var lamp = new GameObject("WarmLight"); lamp.transform.SetParent(root.transform, false); lamp.transform.localPosition = new Vector3(0f, WallH - 0.3f, 0f);
                var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.range = 8f; light.intensity = 2f; light.color = new Color(1f, 0.8f, 0.55f);
            }
            else
            {
                room.LootSpawns = Scatter(root.transform, "LootSpawn", Mathf.RoundToInt(s.W * s.D / 10f), s, rng);
                room.TrapSpawns = Scatter(root.transform, "TrapSpawn", s.W * s.D >= 60 ? 2 : 1, s, rng);
                // 숨을 곳: 모서리(대각선으로 놓여도 벽에 안 닿게 1.5m 안쪽), 방 가운데를 본다
                var hides = new List<Transform>();
                for (int i = 0; i < s.Hides; i++)
                {
                    float sx = i == 0 ? -1f : 1f, sz = i == 0 ? 1f : -1f;
                    var h = Marker(root.transform, "HideSpawn_" + i, new Vector3(sx * (s.W * 0.5f - 1.5f), 0f, sz * (s.D * 0.5f - 1.5f)), null);
                    h.localRotation = Quaternion.LookRotation(-new Vector3(h.localPosition.x, 0f, h.localPosition.z).normalized);
                    hides.Add(h);
                }
                room.HideSpawns = hides.ToArray();
                if (s.Dark) room.DarkZone = Marker(root.transform, "DarkZone", new Vector3(s.W * 0.25f, 0f, -s.D * 0.25f), null);
                if (s.Cat) { room.CatSpawn = Marker(root.transform, "CatSpawn", new Vector3(0f, 0f, 0.5f), "CatSpawn"); }
                if (Mathf.Min(s.W, s.D) >= 8f)
                    Box(root.transform, "Crate", new Vector3(s.W * 0.15f, 0.6f, s.D * 0.15f), new Vector3(1.4f, 1.2f, 1.4f), floorLayer, CrateColor);
            }
            if (s.Spots != null)
                for (int i = 0; i < s.Spots.Length; i++)
                {
                    float a = (i + 0.5f) / s.Spots.Length * Mathf.PI * 2f + 0.4f;
                    var sp = Marker(root.transform, $"Spot_{s.Spots[i]}_{i}", new Vector3(Mathf.Cos(a) * (s.W * 0.5f - 1.5f), 0f, Mathf.Sin(a) * (s.D * 0.5f - 1.5f)), null);
                    sp.localRotation = Quaternion.LookRotation(-sp.localPosition.normalized);
                    sp.gameObject.AddComponent<CatSpot>().EditorSetup(s.Spots[i], 1f);
                }

            grid.EditorSetup(new Vector2(s.W, s.D), plugs, depot);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{RoomDir}/{s.Name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // 길이 1(+Z) 기준 통로 — 스폰 때 루트 Z 스케일 = 실제 길이. 바닥 + 양벽, 폭 = 문틈
        private static GameObject BuildCorridor(string name, Color wallColor)
        {
            var root = new GameObject(name);
            root.AddComponent<Unity.Netcode.NetworkObject>();
            int wallLayer = LayerMask.NameToLayer("NoiseBlocker"), floorLayer = LayerMask.NameToLayer("RoomStatic");
            Box(root.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Door + 2f * WallT, 0.1f, 1f), floorLayer, FloorColor);
            Box(root.transform, "Wall_L", new Vector3(-(Door + WallT) * 0.5f, WallH * 0.5f, 0f), new Vector3(WallT, WallH, 1f), wallLayer, wallColor);
            Box(root.transform, "Wall_R", new Vector3((Door + WallT) * 0.5f, WallH * 0.5f, 0f), new Vector3(WallT, WallH, 1f), wallLayer, wallColor);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{RoomDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, int layer, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = layer; go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = size;
            ZoneTools.Tint(go, color);
            return go;
        }

        private static Transform Marker(Transform parent, string name, Vector3 localPos, string tag)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            if (tag != null) t.gameObject.tag = tag;
            return t;
        }

        // 벽에서 1m 안쪽 무작위 (문 앞 1.2m·가운데 상자 자리는 피함) — 방마다 고정(이름 시드)
        private static Transform[] Scatter(Transform parent, string name, int count, Spec s, System.Random rng)
        {
            var list = new List<Transform>();
            for (int tries = 0; list.Count < count && tries < count * 30; tries++)
            {
                float x = (float)(rng.NextDouble() - 0.5) * (s.W - 2f), z = (float)(rng.NextDouble() - 0.5) * (s.D - 2f);
                var p = new Vector3(x, 0f, z);
                if (Mathf.Abs(x) < 1.2f && Mathf.Abs(z) > s.D * 0.5f - 2.2f) continue;  // 남북 문 앞
                if (Mathf.Abs(z) < 1.2f && Mathf.Abs(x) > s.W * 0.5f - 2.2f) continue;  // 동서 문 앞
                if (Vector3.Distance(p, new Vector3(s.W * 0.15f, 0f, s.D * 0.15f)) < 1.3f) continue; // 상자
                list.Add(Marker(parent, $"{name}_{list.Count}", p, null));
            }
            return list.ToArray();
        }
    }
}
