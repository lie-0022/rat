using System.Collections.Generic;
using RatGame.World;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 킁킁 힌트용 방 그래프 길 찾기 (docs/04 Sniff, 고양이 76). 클라에도 있는 GridRoom 위치·OpenSides로 BFS —
    /// NavMesh는 호스트에서만 구워서 클라가 쓸 수 없다. 목적지 = 식량 창고(DepositZone)가 들어 있는 방.
    /// </summary>
    public static class GridRoute
    {
        private static readonly Vector3[] SideDirs = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        /// <summary>from에서 목적지로 가는 다음 목표점(다음 문, 목적지방이면 창고). roomsLeft = 목적지까지 남은 방 이동 수.</summary>
        public static bool TryNextHint(Vector3 from, out Vector3 target, out int roomsLeft)
        {
            target = default; roomsLeft = -1;
            var depot = Object.FindAnyObjectByType<DepositZone>();
            if (depot == null) return false;
            var rooms = Object.FindObjectsByType<GridRoom>(FindObjectsSortMode.None);
            if (rooms.Length == 0) { target = depot.transform.position; roomsLeft = 0; return true; } // 격자 맵이 아니면 창고로 곧장

            int goal = RoomAt(rooms, depot.transform.position, 0.5f);
            if (goal < 0) return false;
            var dist = DistancesTo(rooms, goal);

            int here = RoomAt(rooms, from, 0.3f);
            if (here < 0) return CorridorHint(rooms, dist, from, out target, out roomsLeft);
            if (dist[here] < 0) return false;
            roomsLeft = dist[here];
            if (here == goal) { target = depot.transform.position; return true; }
            for (int s = 0; s < 4; s++)
            {
                int n = Neighbor(rooms, here, s);
                if (n >= 0 && dist[n] == dist[here] - 1) { target = rooms[here].DoorCenter(s); return true; }
            }
            return false;
        }

        // 통로 안: 가장 가까운 두 방 중 목적지에 가까운 쪽 — 그 방의 문 중 나와 가장 가까운 문(= 이 통로 끝)
        private static bool CorridorHint(GridRoom[] rooms, int[] dist, Vector3 from, out Vector3 target, out int roomsLeft)
        {
            target = default; roomsLeft = -1;
            int best = -1; float bestD = float.MaxValue;
            int second = -1; float secondD = float.MaxValue;
            for (int i = 0; i < rooms.Length; i++)
            {
                float d = Flat(rooms[i].transform.position - from).sqrMagnitude;
                if (d < bestD) { second = best; secondD = bestD; best = i; bestD = d; }
                else if (d < secondD) { second = i; secondD = d; }
            }
            int pick = best;
            if (second >= 0 && dist[second] >= 0 && (dist[best] < 0 || dist[second] < dist[best])) pick = second;
            if (pick < 0 || dist[pick] < 0) return false;
            float doorD = float.MaxValue;
            for (int s = 0; s < 4; s++)
            {
                if ((rooms[pick].OpenSides.Value & (1 << s)) == 0) continue;
                Vector3 door = rooms[pick].DoorCenter(s);
                float d = Flat(door - from).sqrMagnitude;
                if (d < doorD) { doorD = d; target = door; }
            }
            roomsLeft = dist[pick] + 1;
            return doorD < float.MaxValue;
        }

        private static int[] DistancesTo(GridRoom[] rooms, int goal)
        {
            var dist = new int[rooms.Length];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            dist[goal] = 0; queue.Enqueue(goal);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                for (int s = 0; s < 4; s++)
                {
                    int n = Neighbor(rooms, cur, s);
                    if (n < 0 || dist[n] >= 0) continue;
                    dist[n] = dist[cur] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        // 면 s가 열려 있고, 그 방향으로 가장 가까운 방의 반대 면도 열려 있으면 이웃 (방은 회전 없이 격자에 놓인다)
        private static int Neighbor(GridRoom[] rooms, int i, int s)
        {
            if ((rooms[i].OpenSides.Value & (1 << s)) == 0) return -1;
            Vector3 p = rooms[i].transform.position;
            int best = -1; float bestD = float.MaxValue;
            for (int j = 0; j < rooms.Length; j++)
            {
                if (j == i) continue;
                Vector3 d = Flat(rooms[j].transform.position - p);
                float along = Vector3.Dot(d, SideDirs[s]);
                if (along <= 0.1f || (d - SideDirs[s] * along).sqrMagnitude > 0.25f) continue;
                if (along < bestD) { bestD = along; best = j; }
            }
            if (best < 0 || (rooms[best].OpenSides.Value & (1 << ((s + 2) % 4))) == 0) return -1;
            return best;
        }

        private static int RoomAt(GridRoom[] rooms, Vector3 p, float margin)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                Vector3 d = Flat(p - rooms[i].transform.position);
                Vector2 half = rooms[i].Size * 0.5f;
                if (Mathf.Abs(d.x) <= half.x + margin && Mathf.Abs(d.z) <= half.y + margin) return i;
            }
            return -1;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
