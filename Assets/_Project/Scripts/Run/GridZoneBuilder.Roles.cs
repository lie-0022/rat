using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 벽 속 맵의 방·고양이 역할 정하기 (GridZoneBuilder에서 나눔 — 300줄 규칙): 보물방(고양이 84), 아기(75)·문지기(80)·순찰꾼(92).
    /// 전부 호스트가 맵을 지을 때 한 번.
    /// </summary>
    public partial class GridZoneBuilder
    {
        // 깊은 스테이지에서 2마리 이상이면 확률로 한 마리를 아기로 — 엄마·아기 관계(부르면 엄마가 달려옴)는 CatRelation이 붙인다 (고양이 75)
        private void MaybeKitten(int stage, System.Random rng)
        {
            var cats = FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None);
            if (cats.Length < 2 || rng.NextDouble() >= _zone.KittenChanceFor(stage)) return;
            var kitten = cats[rng.Next(cats.Length)];
            if (kitten.ServerMakeKitten()) Log.Dev($"벽 속: 스테이지 {stage} — {kitten.name}를 아기로 (엄마·아기)");
        }

        // 출발방에서 칸 그래프 거리가 가장 먼 막다른 방 — 돌아가는 수고가 보상이 되게
        private int PickTreasure()
        {
            var dist = new int[Plan.Cells.Count];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            dist[0] = 0; queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (var e in Plan.Edges)
                {
                    int next = e.A == cur ? e.B : e.B == cur ? e.A : -1;
                    if (next < 0 || dist[next] >= 0) continue;
                    dist[next] = dist[cur] + 1;
                    queue.Enqueue(next);
                }
            }
            int best = -1;
            for (int i = 0; i < Plan.Cells.Count; i++)
                if (Plan.Cells[i].Role == GridCellRole.DeadEnd && (best < 0 || dist[i] > dist[best])) best = i;
            return best;
        }

        // 깊은 스테이지: 남은 어른 한 마리를 큰길 순찰꾼으로 — 출발·목적지를 뺀 큰길 방 중심마다 지점 (고양이 92)
        private void MaybePatroller(int stage, System.Random rng)
        {
            var adults = new List<AI.CatBrain>();
            foreach (var c in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None)) if (!c.IsKitten && !c.IsGuard) adults.Add(c);
            if (adults.Count == 0 || rng.NextDouble() >= _zone.PatrolChanceFor(stage)) return;
            var path = MainPath();
            var points = new List<Vector3>();
            for (int i = 1; i < path.Count - 1; i++)
            {
                var room = Layout.Rooms[path[i]];
                if (!UnityEngine.AI.NavMesh.SamplePosition(room.transform.position, out var hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                var spot = new GameObject($"PatrolPoint_{points.Count}").transform;
                spot.position = hit.position;
                spot.SetParent(room.transform, true);
                spot.gameObject.AddComponent<AI.CatSpot>();
                points.Add(hit.position);
            }
            if (points.Count < 2) return;
            var patroller = adults[rng.Next(adults.Count)];
            patroller.ServerMakePatroller(points);
            Log.Dev($"벽 속: 스테이지 {stage} — {patroller.name}가 큰길 순찰꾼 (지점 {points.Count})");
        }

        // 출발(0) → 목적지 최단 경로, 배관 빼고 (고양이는 배관을 못 지나서)
        private List<int> MainPath()
        {
            int n = Plan.Cells.Count, goal = Plan.DestinationIndex;
            var prev = new int[n];
            for (int i = 0; i < n; i++) prev[i] = -2;
            var queue = new Queue<int>();
            prev[0] = -1; queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (cur == goal) break;
                foreach (var e in Plan.Edges)
                {
                    if (e.IsPipe) continue;
                    int next = e.A == cur ? e.B : e.B == cur ? e.A : -1;
                    if (next < 0 || prev[next] != -2) continue;
                    prev[next] = cur; queue.Enqueue(next);
                }
            }
            var path = new List<int>();
            for (int at = goal; at >= 0 && prev[at] != -2; at = prev[at]) path.Insert(0, at);
            return path;
        }

        // 깊은 스테이지: 어른 한 마리를 목적지 앞 문지기로 — 목적지 문(배관 아닌 면) 너머 이웃 방 문 안쪽에 초소 (고양이 80)
        private void MaybeGuard(int stage, System.Random rng)
        {
            var dest = Layout.Destination;
            var cats = FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None);
            var adults = new List<AI.CatBrain>();
            foreach (var c in cats) if (!c.IsKitten) adults.Add(c);
            if (dest == null || cats.Length < 2 || adults.Count == 0 || rng.NextDouble() >= _zone.GuardChanceFor(stage)) return;
            int di = Plan.DestinationIndex;
            foreach (var e in Plan.Edges)
            {
                if (e.IsPipe || (e.A != di && e.B != di)) continue;
                int other = e.A == di ? e.B : e.A;
                int side = GridZoneLayout.SideIndex(Plan.Cells[other].Pos - Plan.Cells[di].Pos);
                var neighbor = Layout.Rooms[other];
                Vector3 door = neighbor.DoorCenter((side + 2) % 4);
                Vector3 inward = neighbor.transform.position - door; inward.y = 0f; inward.Normalize();
                var post = new GameObject("GuardPost").transform;
                post.SetPositionAndRotation(door + inward * 1.5f, Quaternion.LookRotation(inward)); // 들어오는 쥐 쪽을 본다
                post.SetParent(neighbor.transform, true);
                post.gameObject.AddComponent<AI.CatSpot>();
                var guard = adults[rng.Next(adults.Count)];
                guard.ServerMakeGuard(post.position);
                Log.Dev($"벽 속: 스테이지 {stage} — {guard.name}가 목적지 앞 문지기");
                return;
            }
        }
    }
}
