using RatGame.Run;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>생성기 v2 (격자 그래프 "벽 속", docs/10) 에디터 도구 — 고양이 62.</summary>
    public static partial class GridZoneTools
    {
        public static readonly GridLayoutPlanner.Settings DefaultSettings = new()
        { MainMin = 5, MainMax = 7, BranchChance = 0.6f, BranchMaxDepth = 2, LoopMinGap = 3 };

        [MenuItem("Tools/RatGame/Zone/Grid Plan Stress (200 seeds)")]
        public static void StressMenu() => Debug.Log("[GRIDSTRESS] " + Stress(200, 1000, DefaultSettings));

        /// <summary>칸 그래프만: 실패·고리 비율·방 수·막다른 방·목적지 거리·이웃 아닌 연결·출발방 파이프.</summary>
        public static string Stress(int count, int firstSeed, GridLayoutPlanner.Settings s)
        {
            int loops = 0, fails = 0, rooms = 0, minR = int.MaxValue, maxR = 0, dead = 0, destDist = 0, badEdges = 0, pipeToStart = 0, startDegreeBad = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var p = GridLayoutPlanner.Plan(firstSeed + i, s);
                if (p.DestinationIndex < 0) { fails++; continue; }
                if (p.HasLoop) loops++;
                rooms += p.Cells.Count; minR = Mathf.Min(minR, p.Cells.Count); maxR = Mathf.Max(maxR, p.Cells.Count);
                foreach (var c in p.Cells) if (c.Role == GridCellRole.DeadEnd) dead++;
                int startDegree = 0;
                foreach (var e in p.Edges)
                {
                    var d = p.Cells[e.A].Pos - p.Cells[e.B].Pos;
                    if (Mathf.Abs(d.x) + Mathf.Abs(d.y) != 1) badEdges++;
                    if (e.A == 0 || e.B == 0) { startDegree++; if (e.IsPipe) pipeToStart++; }
                }
                if (startDegree != 1) startDegreeBad++;
                var dp = p.Cells[p.DestinationIndex].Pos; destDist += Mathf.Abs(dp.x) + Mathf.Abs(dp.y);
            }
            sw.Stop();
            return $"시드 {count}개: 실패 {fails}, 고리 {loops}/{count}, 방 평균 {(float)rooms / count:0.0} ({minR}~{maxR}), 막다른 방 평균 {(float)dead / count:0.0}, " +
                   $"목적지 거리 평균 {(float)destDist / count:0.0}칸, 이웃 아닌 연결 {badEdges}, 출발방 파이프 {pipeToStart}, 출발방 문 1개 아님 {startDegreeBad}, {sw.ElapsedMilliseconds}ms";
        }
    }
}
