using System.Collections.Generic;
using RatGame.Run;
using RatGame.World;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 개발용 배회(-autowander)가 벽 속 미로를 실제로 돌아다니게 (고양이 123). 무작위 방을 골라 킁킁과 같은 길 찾기로 문→문 이동,
    /// 도착하거나 오래 막히면 다음 방. 격자 방이 없는 맵이면 원래 배회(펄린 노이즈). 소크 시험용 — 릴리즈 입력과 무관.
    /// </summary>
    public static class DevWallsWander
    {
        private const float RepickSeconds = 25f;   // 한 방을 이만큼 못 가면 포기하고 다른 방
        private const float HintSeconds = 0.4f;

        private sealed class State { public GridRoom Goal; public float PickedAt; public Vector3 Target; public float NextHint; public Vector3 LastDir; }
        private static readonly Dictionary<ulong, State> States = new();

        public static bool TryDirection(ulong id, Vector3 pos, out Vector3 dir)
        {
            dir = default;
            var rooms = Object.FindObjectsByType<GridRoom>(FindObjectsSortMode.None);
            if (rooms.Length < 2) return false;
            if (!States.TryGetValue(id, out var st)) States[id] = st = new State();
            bool arrived = st.Goal != null && Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(st.Goal.transform.position.x, st.Goal.transform.position.z)) < 1.5f;
            if (st.Goal == null || arrived || Time.time - st.PickedAt > RepickSeconds)
            {
                st.Goal = rooms[Random.Range(0, rooms.Length)];
                st.PickedAt = Time.time;
                st.NextHint = 0f;
            }
            if (Time.time >= st.NextHint)
            {
                st.NextHint = Time.time + HintSeconds;
                if (!GridRoute.TryNextHintTo(pos, st.Goal, out st.Target, out _)) { st.Goal = null; return false; }
            }
            Vector3 d = st.Target - pos; d.y = 0f;
            // 문 앞에 닿아도 아직 방 안으로 쳐서 같은 문을 또 가리킨다 — 가던 방향으로 밀고 지나간다
            if (d.sqrMagnitude < 0.36f) { dir = st.LastDir; return dir.sqrMagnitude > 0.01f; }
            dir = d.normalized;
            st.LastDir = dir;
            return true;
        }
    }
}
