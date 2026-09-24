using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>생성 결과 — 방 순서대로 (0 = 쥐구멍방).</summary>
    public class ZoneLayout
    {
        public readonly List<RoomModule> Rooms = new();
        public RoomModule BonusRoom;
        public int OverlapRejects;   // 겹쳐서 버린 후보 수
        public int Seed;
    }

    /// <summary>
    /// 존 생성기 (docs/10 "조합 랜덤", 2026-09-24 고양이 57 — 1단계: 배치만). 호스트 전용.
    /// 쥐구멍방 → 중간방을 중복 없이 뽑아 DoorSocket으로 선형 접합, 바운즈가 겹치면 다음 후보, 5번 실패하면 짧은 존.
    /// 방 모양을 직사각형으로만 만드는 게 겹침 문제의 전부 (docs/10).
    /// </summary>
    public class ZoneGenerator
    {
        private const int MaxRejects = 5;
        private const float OverlapSkin = 0.05f; // 벽끼리 맞닿는 건 겹침이 아니다

        public ZoneLayout Generate(ZoneDefinitionSO def, BalanceConfigSO balance, int seed, int zoneIndex, Transform parent)
        {
            var rng = new System.Random(seed + zoneIndex * 7919);
            var layout = new ZoneLayout { Seed = seed };

            var first = Place(def.RatHoleRoom, parent, Vector3.zero, Quaternion.identity);
            layout.Rooms.Add(first);

            int want = rng.Next(balance.RoomModulesPerZone.x, balance.RoomModulesPerZone.y + 1);
            var pool = new List<GameObject>(def.MiddleRoomPool);
            var current = first;
            int rejects = 0;
            while (layout.Rooms.Count - 1 < want && pool.Count > 0 && rejects < MaxRejects)
            {
                var exit = PickExit(current, rng, reserveForBonus: false);
                if (exit == null) break;
                int pick = rng.Next(pool.Count);
                var room = TryAttach(pool[pick], exit, parent, layout);
                if (room == null) { rejects++; layout.OverlapRejects++; if (pool.Count > 1) { var t = pool[pick]; pool.RemoveAt(pick); pool.Add(t); } continue; }
                pool.RemoveAt(pick);
                MarkUsed(current, exit);
                layout.Rooms.Add(room);
                current = room;
            }

            // 보너스방: 쓰이지 않은 출구가 있는 방 중 하나에 확률로 (막다른 고가치·고위험)
            if (def.BonusRoomPool != null && def.BonusRoomPool.Length > 0 && rng.NextDouble() < def.BonusRoomChance)
            {
                for (int i = layout.Rooms.Count - 1; i >= 0 && layout.BonusRoom == null; i--)
                {
                    var host = layout.Rooms[i];
                    foreach (var exit in FreeExits(host))
                    {
                        var bonus = TryAttach(def.BonusRoomPool[rng.Next(def.BonusRoomPool.Length)], exit, parent, layout);
                        if (bonus == null) { layout.OverlapRejects++; continue; }
                        MarkUsed(host, exit);
                        layout.BonusRoom = bonus;
                        break;
                    }
                }
            }
            Log.Dev($"존 생성: 시드 {seed} 존 {zoneIndex} — 방 {layout.Rooms.Count}{(layout.BonusRoom != null ? " + 보너스" : "")}, 겹침 버림 {layout.OverlapRejects}");
            return layout;
        }

        private readonly Dictionary<RoomModule, HashSet<DoorSocket>> _used = new();

        private void MarkUsed(RoomModule room, DoorSocket exit)
        {
            if (!_used.TryGetValue(room, out var set)) _used[room] = set = new HashSet<DoorSocket>();
            set.Add(exit);
        }

        private IEnumerable<DoorSocket> FreeExits(RoomModule room)
        {
            _used.TryGetValue(room, out var set);
            foreach (var e in room.Exits) if (e != null && (set == null || !set.Contains(e))) yield return e;
        }

        private DoorSocket PickExit(RoomModule room, System.Random rng, bool reserveForBonus)
        {
            var free = new List<DoorSocket>(FreeExits(room));
            return free.Count == 0 ? null : free[rng.Next(free.Count)];
        }

        private RoomModule TryAttach(GameObject prefab, DoorSocket exit, Transform parent, ZoneLayout layout)
        {
            var room = Place(prefab, parent, Vector3.zero, Quaternion.identity);
            // 회전: 새 방 Entry의 +Z(바깥)가 출구의 -Z를 보게 — 수평(yaw)만. FromToRotation은 180°일 때 축을 멋대로 골라 방이 눕는다
            Vector3 from = room.Entry.transform.forward; from.y = 0f;
            Vector3 to = -exit.transform.forward; to.y = 0f;
            float yaw = Vector3.SignedAngle(from, to, Vector3.up);
            room.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * room.transform.rotation;
            room.transform.position += exit.transform.position - room.Entry.transform.position;
            Physics.SyncTransforms();
            if (Overlaps(room, layout))
            {
                Object.DestroyImmediate(room.gameObject);
                return null;
            }
            return room;
        }

        private static bool Overlaps(RoomModule room, ZoneLayout layout)
        {
            room.GetBox(out var c, out var half, out var rot);
            half -= Vector3.one * OverlapSkin;
            foreach (var other in layout.Rooms)
            {
                other.GetBox(out var oc, out var ohalf, out var orot);
                if (BoxesOverlap(c, half, rot, oc, ohalf, orot)) return true;
            }
            if (layout.BonusRoom != null)
            {
                layout.BonusRoom.GetBox(out var bc, out var bhalf, out var brot);
                if (BoxesOverlap(c, half, rot, bc, bhalf, brot)) return true;
            }
            return false;
        }

        // 방은 수평으로 90° 단위로만 돈다 → 축 정렬 XZ 사각형 교차로 충분
        private static bool BoxesOverlap(Vector3 ac, Vector3 ah, Quaternion ar, Vector3 bc, Vector3 bh, Quaternion br)
        {
            Vector2 aExt = HorizontalExtents(ah, ar), bExt = HorizontalExtents(bh, br);
            return Mathf.Abs(ac.x - bc.x) < aExt.x + bExt.x && Mathf.Abs(ac.z - bc.z) < aExt.y + bExt.y;
        }

        private static Vector2 HorizontalExtents(Vector3 half, Quaternion rot)
        {
            Vector3 x = rot * new Vector3(half.x, 0f, 0f), z = rot * new Vector3(0f, 0f, half.z);
            return new Vector2(Mathf.Abs(x.x) + Mathf.Abs(z.x), Mathf.Abs(x.z) + Mathf.Abs(z.z));
        }

        private static RoomModule Place(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
        {
            // 부모 기준 — 존을 어디에 두든(스트레스 테스트는 멀리) 방이 부모를 따라간다
            var go = parent != null
                ? Object.Instantiate(prefab, parent.TransformPoint(pos), parent.rotation * rot, parent)
                : Object.Instantiate(prefab, pos, rot);
            go.name = prefab.name;
            return go.GetComponent<RoomModule>();
        }
    }
}
