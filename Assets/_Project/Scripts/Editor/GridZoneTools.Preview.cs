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
    /// <summary>생성기 v2 미리보기·검사 (고양이 62): 임시 씬에 한 판 깔고 겹침·NavMesh(출발→목적지, 전 방 도달)를 재고 위에서 본 그림을 PNG로.</summary>
    public static partial class GridZoneTools
    {
        [MenuItem("Tools/RatGame/Zone/Preview Wall Layout (seed 1000)")]
        public static void PreviewMenu() => Debug.Log("[GRIDPREVIEW] " + Preview(1000, "Temp/grid_preview_1000.png"));

        public static string Preview(int seed, string pngPath)
        {
            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            if (zone == null) return "Zone_Walls 없음 — Create Wall Rooms 먼저";
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var prevActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);
            try
            {
                var plan = GridLayoutPlanner.Plan(seed, GridZoneLayout.SettingsOf(zone));
                var root = new GameObject("GridPreview").transform;
                Vector3 origin = new(2000f, 0f, 2000f); // 다른 씬 NavMesh와 안 섞이게 멀리
                var layout = GridZoneLayout.Place(zone, plan, origin, new System.Random(seed ^ 0x6d),
                    prefab => { var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp); go.transform.SetParent(root, true); return go; });
                for (int i = 0; i < layout.Rooms.Count; i++) layout.Rooms[i].EditorApply(layout.Sides[i]);
                Physics.SyncTransforms();

                // 겹침: 방끼리·통로끼리 (통로 끝이 벽 두께만큼 방에 물리는 건 정상)
                int overlaps = 0;
                for (int a = 0; a < layout.Rooms.Count; a++)
                    for (int b = a + 1; b < layout.Rooms.Count; b++)
                    {
                        var A = layout.Rooms[a].GetComponent<RoomModule>().WorldBounds; var B = layout.Rooms[b].GetComponent<RoomModule>().WorldBounds;
                        A.Expand(-0.1f); B.Expand(-0.1f);
                        if (A.Intersects(B)) overlaps++;
                    }

                var surface = root.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.layerMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker"); // 실제 스테이지(GridZoneBuilder)와 같게 — 장식(Default)은 빠진다
                surface.BuildNavMesh();
                int unreachable = 0; bool destOk = false;
                NavMesh.SamplePosition(layout.Start.transform.position, out var hs, 2f, NavMesh.AllAreas);
                foreach (var r in layout.Rooms)
                {
                    var path = new NavMeshPath();
                    // 목적지방은 안전지대라 고양이 길이 없다(고양이 72) — 문 밖까지 오면 된다
                    Vector3 target = r.transform.position;
                    if (r == layout.Destination) target = DestDoorOutside(r, layout, plan);
                    NavMesh.SamplePosition(target, out var ht, 3f, NavMesh.AllAreas);
                    NavMesh.CalculatePath(hs.position, ht.position, NavMesh.AllAreas, path);
                    bool ok = path.status == NavMeshPathStatus.PathComplete;
                    if (!ok) unreachable++;
                    if (r == layout.Destination) destOk = ok;
                }
                RenderTop(root, pngPath);
                surface.RemoveData();
                return $"시드 {seed}: 방 {layout.Rooms.Count}, 통로 {layout.Corridors.Count}(고리 {(plan.HasLoop ? 1 : 0)}), 방 겹침 {overlaps}, 출발→목적지 {(destOk ? "OK" : "막힘")}, 못 가는 방 {unreachable}, 그림 {pngPath}\n{plan.ToAscii()}";
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(temp, true);
            }
        }

        private static Vector3 DestDoorOutside(GridRoom dest, GridZoneLayout.Result layout, GridPlan plan)
        {
            byte sides = layout.Sides[plan.DestinationIndex]; int side = 0; while (side < 4 && (sides & (1 << side)) == 0) side++;
            Vector3 door = dest.DoorCenter(side), outward = door - dest.transform.position; outward.y = 0f;
            return door + outward.normalized * 1.5f;
        }

        private static void RenderTop(Transform root, string pngPath)
        {
            var bounds = new Bounds(root.position, Vector3.zero); bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            var camGo = new GameObject("TopCam"); var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x * 9f / 16f) + 2f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            camGo.transform.SetPositionAndRotation(bounds.center + Vector3.up * 60f, Quaternion.Euler(90f, 0f, 0f));
            cam.farClipPlane = 200f;
            var light = new GameObject("TopLight").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); RenderTexture.active = null;
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(tex); Object.DestroyImmediate(camGo); Object.DestroyImmediate(light.gameObject);
        }
    }
}
