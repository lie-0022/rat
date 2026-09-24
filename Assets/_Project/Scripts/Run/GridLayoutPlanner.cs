using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RatGame.Run
{
    public enum GridCellRole : byte { Start, Main, Branch, DeadEnd, Destination }

    /// <summary>칸 하나 = 방 하나 (docs/10 생성기 v2).</summary>
    public struct GridCell
    {
        public Vector2Int Pos;
        public GridCellRole Role;
        public int MainIndex;   // 주 경로 순번 (곁가지는 매달린 주 경로 방의 순번)
    }

    /// <summary>이웃 칸 연결. IsPipe = 고리를 닫는 좁은 통로.</summary>
    public struct GridEdge
    {
        public int A, B;
        public bool IsPipe;
    }

    public class GridPlan
    {
        public readonly List<GridCell> Cells = new();
        public readonly List<GridEdge> Edges = new();
        public int DestinationIndex = -1;
        public int Attempts;
        public bool HasLoop;

        public int IndexAt(Vector2Int p) { for (int i = 0; i < Cells.Count; i++) if (Cells[i].Pos == p) return i; return -1; }

        /// <summary>면 비트 (N=1, E=2, S=4, W=8) — 이 칸에서 열린 문.</summary>
        public byte OpenSides(int cell)
        {
            byte bits = 0;
            foreach (var e in Edges)
            {
                if (e.A != cell && e.B != cell) continue;
                Vector2Int d = Cells[e.A == cell ? e.B : e.A].Pos - Cells[cell].Pos;
                bits |= SideBit(d);
            }
            return bits;
        }

        /// <summary>그 칸의 열린 면 중 쥐 전용 배관인 면 (고양이 79).</summary>
        public byte PipeSides(int cell)
        {
            byte bits = 0;
            foreach (var e in Edges)
            {
                if (!e.IsPipe || (e.A != cell && e.B != cell)) continue;
                bits |= SideBit(Cells[e.A == cell ? e.B : e.A].Pos - Cells[cell].Pos);
            }
            return bits;
        }

        public static byte SideBit(Vector2Int d) =>
            d == Vector2Int.up ? (byte)1 : d == Vector2Int.right ? (byte)2 : d == Vector2Int.down ? (byte)4 : (byte)8;

        /// <summary>글자 지도 (디버그·계획서용). S 출발, D 목적지, M 주 경로, b 곁가지, x 막다른 방, 문 = - |, 파이프 = = ‖.</summary>
        public string ToAscii()
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in Cells) { minX = Mathf.Min(minX, c.Pos.x); maxX = Mathf.Max(maxX, c.Pos.x); minY = Mathf.Min(minY, c.Pos.y); maxY = Mathf.Max(maxY, c.Pos.y); }
            var sb = new StringBuilder();
            for (int y = maxY; y >= minY; y--)
            {
                var row = new StringBuilder(); var below = new StringBuilder();
                for (int x = minX; x <= maxX; x++)
                {
                    int i = IndexAt(new Vector2Int(x, y));
                    row.Append(i < 0 ? ' ' : Glyph(Cells[i].Role));
                    row.Append(Link(new Vector2Int(x, y), new Vector2Int(x + 1, y), '-', '='));
                    below.Append(Link(new Vector2Int(x, y), new Vector2Int(x, y - 1), '|', '#')).Append(' ');
                }
                sb.AppendLine(row.ToString().TrimEnd());
                if (y > minY) sb.AppendLine(below.ToString().TrimEnd());
            }
            return sb.ToString();
        }

        private char Link(Vector2Int a, Vector2Int b, char door, char pipe)
        {
            int ia = IndexAt(a), ib = IndexAt(b);
            if (ia < 0 || ib < 0) return ' ';
            foreach (var e in Edges)
                if ((e.A == ia && e.B == ib) || (e.A == ib && e.B == ia)) return e.IsPipe ? pipe : door;
            return ' ';
        }

        private static char Glyph(GridCellRole r) => r switch
        {
            GridCellRole.Start => 'S', GridCellRole.Destination => 'D', GridCellRole.Main => 'M', GridCellRole.Branch => 'b', _ => 'x',
        };
    }

    /// <summary>
    /// 생성기 v2 칸 그래프 (docs/10, 2026-09-24 고양이 62) — 맵 시안 "벽 속": 출발 → 주 경로 → 목적지, 곁가지 막다른 방, 고리 1개.
    /// 유니티 씬 없이 시드만으로 돈다(스트레스 테스트가 빠르게). 호스트만 쓰고 결과는 스폰으로 전달 — 클라는 계산하지 않는다.
    /// </summary>
    public static class GridLayoutPlanner
    {
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public struct Settings
        {
            public int MainMin, MainMax;       // 출발·목적지 사이 방 수
            public float BranchChance;
            public int BranchMaxDepth;
            public int LoopMinGap;             // 고리로 이을 두 방의 주 경로 순번 차 최소
        }

        public static GridPlan Plan(int seed, Settings s)
        {
            var rng = new System.Random(seed);
            GridPlan plan = null;
            for (int attempt = 1; attempt <= 30; attempt++)
            {
                plan = TryPlan(rng, s);
                plan.Attempts = attempt;
                if (plan.DestinationIndex >= 0) break;
            }
            return plan;
        }

        private static GridPlan TryPlan(System.Random rng, Settings s)
        {
            var plan = new GridPlan();
            var used = new HashSet<Vector2Int>();
            int mainRooms = rng.Next(s.MainMin, s.MainMax + 1);

            // 1) 주 경로: 출발에서 멀어지는 쪽을 더 자주 고르는 자기 회피 걸음 (말리지 않게)
            Add(plan, used, Vector2Int.zero, GridCellRole.Start, 0);
            Vector2Int cur = Vector2Int.zero;
            for (int step = 1; step <= mainRooms + 1; step++)
            {
                var next = PickNeighbor(rng, used, cur, Vector2Int.zero, 3f);
                if (!next.HasValue) return plan; // 막힘 — 다시
                var role = step == mainRooms + 1 ? GridCellRole.Destination : GridCellRole.Main;
                Add(plan, used, next.Value, role, step);
                Connect(plan, plan.Cells.Count - 2, plan.Cells.Count - 1, false);
                cur = next.Value;
            }
            plan.DestinationIndex = plan.Cells.Count - 1;

            // 2) 곁가지: 주 경로 방(출발·목적지 제외)마다 확률로 1~깊이만큼, 끝은 막다른 방
            int mainCount = plan.Cells.Count;
            for (int i = 1; i < mainCount - 1; i++)
            {
                if (rng.NextDouble() >= s.BranchChance) continue;
                int depth = rng.Next(1, s.BranchMaxDepth + 1);
                int from = i;
                for (int d = 0; d < depth; d++)
                {
                    var next = PickNeighbor(rng, used, plan.Cells[from].Pos, plan.Cells[from].Pos, 1f);
                    if (!next.HasValue) break;
                    Add(plan, used, next.Value, GridCellRole.Branch, plan.Cells[i].MainIndex);
                    Connect(plan, from, plan.Cells.Count - 1, false);
                    from = plan.Cells.Count - 1;
                }
                if (from != i) { var c = plan.Cells[from]; c.Role = GridCellRole.DeadEnd; plan.Cells[from] = c; }
            }

            // 3) 고리: 이웃인데 안 이어진 두 방 중 주 경로 순번 차가 큰 쌍 하나를 파이프로 (출발방 제외 — 입구는 하나)
            var candidates = new List<(int, int)>();
            for (int a = 1; a < plan.Cells.Count; a++)
                foreach (var dir in Dirs)
                {
                    int b = plan.IndexAt(plan.Cells[a].Pos + dir);
                    if (b <= a || b == 0 || Connected(plan, a, b)) continue;
                    if (Mathf.Abs(plan.Cells[a].MainIndex - plan.Cells[b].MainIndex) >= s.LoopMinGap) candidates.Add((a, b));
                }
            if (candidates.Count > 0)
            {
                var (a, b) = candidates[rng.Next(candidates.Count)];
                Connect(plan, a, b, true);
                plan.HasLoop = true;
            }
            else plan.HasLoop = GrowLoop(plan, used, rng, s); // 자연히 안 생기면 빈 칸으로 짧은 샛길을 내서 닫는다

            // 역할 다시 매기기 — 고리가 막다른 방에 붙으면 더는 막다른 방이 아니다
            for (int i = 0; i < plan.Cells.Count; i++)
            {
                var c = plan.Cells[i];
                if (c.Role != GridCellRole.Branch && c.Role != GridCellRole.DeadEnd) continue;
                int degree = 0; foreach (var e in plan.Edges) if (e.A == i || e.B == i) degree++;
                c.Role = degree <= 1 ? GridCellRole.DeadEnd : GridCellRole.Branch;
                plan.Cells[i] = c;
            }
            return plan;
        }

        // 주 경로 방 하나에서 빈 칸 1~3개짜리 샛길을 내어, 순번이 loopMinGap 이상 떨어진 방 옆에 닿으면 파이프로 잇는다
        private static bool GrowLoop(GridPlan plan, HashSet<Vector2Int> used, System.Random rng, Settings s)
        {
            var starts = new List<int>();
            for (int i = 1; i < plan.Cells.Count; i++) if (plan.Cells[i].Role == GridCellRole.Main) starts.Add(i);
            for (int k = starts.Count - 1; k > 0; k--) { int r = rng.Next(k + 1); (starts[k], starts[r]) = (starts[r], starts[k]); }
            foreach (int from in starts)
            {
                int fromIndex = plan.Cells[from].MainIndex;
                // 너비 우선 (깊이 3) — 이전 칸 기록
                var prev = new Dictionary<Vector2Int, Vector2Int>();
                var frontier = new List<Vector2Int> { plan.Cells[from].Pos };
                for (int depth = 1; depth <= 3; depth++)
                {
                    var next = new List<Vector2Int>();
                    foreach (var at in frontier)
                        foreach (var d in Dirs)
                        {
                            var p = at + d;
                            if (used.Contains(p) || prev.ContainsKey(p)) continue;
                            prev[p] = at; next.Add(p);
                            foreach (var d2 in Dirs)
                            {
                                int j = plan.IndexAt(p + d2);
                                if (j <= 0 || j == from) continue;
                                if (Mathf.Abs(plan.Cells[j].MainIndex - fromIndex) < s.LoopMinGap) continue;
                                // 길 만들기: from → … → p, p ↔ j 파이프
                                var path = new List<Vector2Int>();
                                for (var q = p; q != plan.Cells[from].Pos; q = prev[q]) path.Add(q);
                                path.Reverse();
                                int last = from;
                                foreach (var q in path)
                                {
                                    Add(plan, used, q, GridCellRole.Branch, fromIndex);
                                    Connect(plan, last, plan.Cells.Count - 1, false);
                                    last = plan.Cells.Count - 1;
                                }
                                Connect(plan, last, j, true);
                                return true;
                            }
                        }
                    frontier = next;
                }
            }
            return false;
        }

        // 빈 이웃 칸 하나. awayFrom에서 멀어지는 쪽 가중치 forwardBias
        private static Vector2Int? PickNeighbor(System.Random rng, HashSet<Vector2Int> used, Vector2Int at, Vector2Int awayFrom, float forwardBias)
        {
            float total = 0f; var options = new List<(Vector2Int, float)>();
            int baseDist = Manhattan(at, awayFrom);
            foreach (var d in Dirs)
            {
                var p = at + d;
                if (used.Contains(p)) continue;
                int dist = Manhattan(p, awayFrom);
                float w = dist > baseDist ? forwardBias : dist == baseDist ? 1f : 0.3f;
                options.Add((p, w)); total += w;
            }
            if (options.Count == 0) return null;
            double roll = rng.NextDouble() * total;
            foreach (var (p, w) in options) { roll -= w; if (roll <= 0) return p; }
            return options[options.Count - 1].Item1;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static void Add(GridPlan plan, HashSet<Vector2Int> used, Vector2Int p, GridCellRole role, int mainIndex)
        {
            plan.Cells.Add(new GridCell { Pos = p, Role = role, MainIndex = mainIndex });
            used.Add(p);
        }

        private static void Connect(GridPlan plan, int a, int b, bool pipe) => plan.Edges.Add(new GridEdge { A = a, B = b, IsPipe = pipe });

        private static bool Connected(GridPlan plan, int a, int b)
        {
            foreach (var e in plan.Edges) if ((e.A == a && e.B == b) || (e.A == b && e.B == a)) return true;
            return false;
        }
    }
}
