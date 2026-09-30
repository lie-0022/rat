using System.Collections.Generic;
using NUnit.Framework;
using RatGame.Data;
using RatGame.Run;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 벽 속 맵 설계도 (docs/10 생성기 v2, 고양이 290) — 실제 존 설정(Zone_Walls)으로 시드 200개:
    /// 같은 시드 = 같은 맵, 한 칸에 방 하나, 이웃 칸끼리만 이음, 출발에서 모든 방·목적지에 닿음.
    /// </summary>
    public class MapPlanTests
    {
        private const int Seeds = 200;

        private static GridLayoutPlanner.Settings Settings()
        {
            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>("Assets/_Project/Data/Zones/Zone_Walls.asset");
            Assert.NotNull(zone, "Zone_Walls 없음");
            return GridZoneLayout.SettingsOf(zone);
        }

        [Test]
        public void 같은_시드면_같은_맵()
        {
            var s = Settings();
            for (int seed = 1; seed <= 20; seed++)
            {
                var a = GridLayoutPlanner.Plan(seed, s);
                var b = GridLayoutPlanner.Plan(seed, s);
                Assert.AreEqual(a.Cells.Count, b.Cells.Count, $"시드 {seed}");
                for (int i = 0; i < a.Cells.Count; i++) Assert.AreEqual(a.Cells[i].Pos, b.Cells[i].Pos, $"시드 {seed} 칸 {i}");
                Assert.AreEqual(a.Edges.Count, b.Edges.Count, $"시드 {seed}");
                Assert.AreEqual(a.DestinationIndex, b.DestinationIndex, $"시드 {seed}");
            }
        }

        [Test]
        public void 겹침_없고_이웃끼리만_이음_모두_닿음()
        {
            var s = Settings();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var p = GridLayoutPlanner.Plan(seed, s);
                Assert.Greater(p.Cells.Count, 1, $"시드 {seed}");
                Assert.That(p.DestinationIndex, Is.InRange(0, p.Cells.Count - 1), $"시드 {seed} 목적지");

                var seen = new HashSet<Vector2Int>();
                foreach (var c in p.Cells) Assert.IsTrue(seen.Add(c.Pos), $"시드 {seed}: 칸 겹침 {c.Pos}");

                var adj = new List<int>[p.Cells.Count];
                for (int i = 0; i < adj.Length; i++) adj[i] = new List<int>();
                foreach (var e in p.Edges)
                {
                    var d = p.Cells[e.A].Pos - p.Cells[e.B].Pos;
                    Assert.AreEqual(1, Mathf.Abs(d.x) + Mathf.Abs(d.y), $"시드 {seed}: 이웃 아닌 칸을 이음 {e.A}-{e.B}");
                    adj[e.A].Add(e.B); adj[e.B].Add(e.A);
                }

                var reached = new bool[p.Cells.Count];
                var queue = new Queue<int>();
                queue.Enqueue(0); reached[0] = true;
                while (queue.Count > 0)
                    foreach (int n in adj[queue.Dequeue()])
                        if (!reached[n]) { reached[n] = true; queue.Enqueue(n); }
                for (int i = 0; i < reached.Length; i++) Assert.IsTrue(reached[i], $"시드 {seed}: 칸 {i}({p.Cells[i].Role})에 못 닿음");
            }
        }
    }
}
