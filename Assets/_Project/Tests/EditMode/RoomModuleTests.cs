using System.Collections.Generic;
using NUnit.Framework;
using RatGame.World;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 방 프리팹 규약 (docs/10, docs/14 "Room Module Checker", 고양이 331) — 방을 고치거나 Create Wall Rooms를 다시 돌린 뒤
    /// 빈 자리·방 밖 자리·출발방 인원 부족이 생기면 맵 생성 중에야 드러난다. 전리품 자리 개수는 방 종류마다 달라 보지 않는다.
    /// </summary>
    public class RoomModuleTests
    {
        private static int MaxPlayers()
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            return new SerializedObject(so).FindProperty("_maxPlayers").intValue;
        }

        [Test]
        public void 방_프리팹_자리가_비지_않고_방_안에_있음()
        {
            int maxPlayers = MaxPlayers();
            var problems = new List<string>();
            int rooms = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Rooms" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var room = go.GetComponent<RoomModule>();
                if (room == null) continue; // 불빛·복도 조각 같은 부품
                rooms++;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                var box = go.GetComponent<BoxCollider>();
                if (box == null) { problems.Add($"{name}: 루트 BoxCollider(바운즈) 없음"); continue; }
                var bounds = new Bounds(go.transform.TransformPoint(box.center), Vector3.Scale(box.size, go.transform.lossyScale));
                bounds.Expand(0.1f);

                void Check(string label, IList<Transform> points)
                {
                    if (points == null) return;
                    for (int i = 0; i < points.Count; i++)
                    {
                        if (points[i] == null) problems.Add($"{name}: {label}[{i}] 빈 칸");
                        else if (!bounds.Contains(points[i].position)) problems.Add($"{name}: {label}[{i}] 방 밖 {points[i].position}");
                    }
                }
                Check("LootSpawns", room.LootSpawns);
                Check("TrapSpawns", room.TrapSpawns);
                Check("PlayerSpawns", room.PlayerSpawns);
                Check("HideSpawns", room.HideSpawns);
                if (room.CatSpawn != null) Check("CatSpawn", new[] { room.CatSpawn });

                bool start = room.PlayerSpawns != null && room.PlayerSpawns.Length > 0;
                if (start && room.PlayerSpawns.Length < maxPlayers) problems.Add($"{name}: 출발 자리 {room.PlayerSpawns.Length} < 최대 인원 {maxPlayers}");
                if (start && room.TrapSpawns != null && room.TrapSpawns.Length > 0) problems.Add($"{name}: 출발방에 함정 자리 (docs/10 — 쥐구멍방엔 없음)");
                // 1번째 루프 방(부엌·지하실)은 소켓으로 잇는다 — 쥐구멍방 말고는 입구가 있어야. 벽 속 방은 칸 격자라 소켓 없음
                if (!path.Contains("/Walls/") && room.RatHole == null && room.Entry == null) problems.Add($"{name}: 입구 소켓 없음");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.Greater(rooms, 10);
        }
    }
}
