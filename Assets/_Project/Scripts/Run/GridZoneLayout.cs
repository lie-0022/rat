using System;
using System.Collections.Generic;
using RatGame.Data;
using RatGame.World;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 칸 그래프 → 방·통로 배치 (docs/10 생성기 v2, 고양이 62). 방은 칸 가운데, 통로는 이웃 두 방의 마주 보는 문 사이(길이 = 루트 Z 스케일).
    /// 만드는 방법(make)을 받아서 에디터 미리보기(프리팹 인스턴스)와 호스트 스폰이 같은 배치를 쓴다.
    /// </summary>
    public static class GridZoneLayout
    {
        public class Result
        {
            public readonly List<GridRoom> Rooms = new();      // plan.Cells 순서
            public readonly List<byte> Sides = new();          // 아래 4비트 열린 면, 위 4비트 그중 배관 (GridRoom.OpenSides 형식)
            public readonly List<GameObject> Corridors = new();
            public GridRoom Start => Rooms.Count > 0 ? Rooms[0] : null;
            public GridRoom Destination;
        }

        public static Result Place(GridZoneSO zone, GridPlan plan, Vector3 origin, System.Random rng, Func<GameObject, GameObject> make)
        {
            var result = new Result();
            for (int i = 0; i < plan.Cells.Count; i++)
            {
                var cell = plan.Cells[i];
                var prefab = PrefabFor(zone, cell.Role, rng);
                var go = make(prefab);
                go.transform.SetPositionAndRotation(origin + new Vector3(cell.Pos.x * zone.CellSize, 0f, cell.Pos.y * zone.CellSize), Quaternion.identity);
                var room = go.GetComponent<GridRoom>();
                result.Rooms.Add(room);
                result.Sides.Add((byte)(plan.OpenSides(i) | (plan.PipeSides(i) << 4)));
                if (i == plan.DestinationIndex) result.Destination = room;
            }
            foreach (var e in plan.Edges)
            {
                var a = result.Rooms[e.A]; var b = result.Rooms[e.B];
                int sideA = SideIndex(plan.Cells[e.B].Pos - plan.Cells[e.A].Pos);
                Vector3 doorA = a.DoorCenter(sideA), doorB = b.DoorCenter((sideA + 2) % 4);
                float length = Vector3.Distance(doorA, doorB);
                if (length < 0.05f) continue; // 방이 칸을 꽉 채우면 통로 없이 붙는다
                var go = make(e.IsPipe ? zone.Pipe : zone.Corridor);
                go.transform.SetPositionAndRotation((doorA + doorB) * 0.5f, Quaternion.LookRotation(doorB - doorA));
                go.transform.localScale = new Vector3(1f, 1f, length);
                result.Corridors.Add(go);
            }
            return result;
        }

        private static GameObject PrefabFor(GridZoneSO zone, GridCellRole role, System.Random rng) => role switch
        {
            GridCellRole.Start => zone.StartRoom,
            GridCellRole.Destination => zone.DestinationRoom,
            GridCellRole.Main => Pick(zone.MainRoomPool, rng),
            GridCellRole.Branch => Pick(zone.BranchRoomPool, rng),
            _ => Pick(zone.DeadEndRoomPool, rng),
        };

        private static GameObject Pick(GameObject[] pool, System.Random rng) => pool[rng.Next(pool.Length)];

        /// <summary>칸 방향 → 면 번호 (0 N, 1 E, 2 S, 3 W).</summary>
        public static int SideIndex(Vector2Int d) => d == Vector2Int.up ? 0 : d == Vector2Int.right ? 1 : d == Vector2Int.down ? 2 : 3;

        /// <summary>설정 묶음 (테마 에셋 → 플래너).</summary>
        public static GridLayoutPlanner.Settings SettingsOf(GridZoneSO zone) => new()
        {
            MainMin = zone.MainPathMin, MainMax = zone.MainPathMax, BranchChance = zone.BranchChance,
            BranchMaxDepth = zone.BranchMaxDepth, LoopMinGap = zone.LoopMinGap,
        };
    }
}
