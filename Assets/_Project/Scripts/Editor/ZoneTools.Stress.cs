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
    /// <summary>존 생성기 스트레스 테스트 (고양이 57) — ZoneTools에서 분리 (300줄 규칙, 고양이 60).</summary>
    public static partial class ZoneTools
    {
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
