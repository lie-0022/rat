using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.Run
{
    /// <summary>
    /// 방 채우기 (docs/10) — 전리품(테이블)·반지·함정·숨을 곳·어둠·고양이. v1(ZoneBuilder, 부엌)과 v2(GridZoneBuilder, 벽 속)가 같이 쓴다.
    /// 호스트 전용, NavMesh를 구운 뒤 부른다. 2026-09-24 ZoneBuilder에서 분리 (고양이 62).
    /// </summary>
    public class ZonePopulator
    {
        public readonly ZoneDefinitionSO Pop;
        public readonly List<RoomModule> Rooms;
        public readonly RoomModule SafeRoom;   // 함정 없는 방 (시작방)
        public readonly RoomModule BonusRoom;  // 전리품 전부·고가치 (없으면 null)

        public int LootSpawned { get; private set; }
        public int LootValue { get; private set; }
        public int TrapsSpawned { get; private set; }
        public int CatsSpawned { get; private set; }
        public int HidesSpawned { get; private set; }
        public int DarkSpawned { get; private set; }

        /// <summary>새 루프 깊이별 덮어쓰기 (고양이 66). 음수면 테이블 값.</summary>
        public int CatCountOverride = -1;
        public float TrapRatioOverride = -1f;
        public float BonusTrapRatio = -1f; // >=0이면 보너스방 함정 비율 (벽 속 보물방 — 고양이 84)
        public float BonusBigWeight = 3f;  // 보너스방 대형·특수 가중 배율 (v1 3, 벽 속 보물방은 테마 에셋에서)

        public ZonePopulator(ZoneDefinitionSO pop, List<RoomModule> rooms, RoomModule safeRoom, RoomModule bonusRoom)
        {
            Pop = pop; Rooms = rooms; SafeRoom = safeRoom; BonusRoom = bonusRoom;
        }

        /// <summary>전부 순서대로 — 숨을 곳은 고양이보다 먼저(고양이가 스폰 때 상자 입구 스팟을 모은다).</summary>
        public void PopulateAll(System.Random rng, int zoneIndex)
        {
            Loot(rng, zoneIndex);
            if (Pop.LootTable != null && Pop.LootTable.Entries != null) Ring(rng);
            Traps(rng);
            HidesAndDark(rng);
            Cats(rng);
        }

        public void Loot(System.Random rng, int zoneIndex)
        {
            var table = Pop.LootTable;
            if (table == null || table.Entries == null) return;
            var counts = new Dictionary<LootItemSO, int>();
            // 보너스방을 먼저 — 대형은 맵 전체 상한(Max)이 있어서 뒤에 채우면 이미 동나 있다
            var order = new List<RoomModule>(Rooms);
            if (BonusRoom != null && order.Remove(BonusRoom)) order.Insert(0, BonusRoom);
            foreach (var room in order)
            {
                bool bonus = room == BonusRoom;
                var points = new List<Transform>(room.LootSpawns);
                Shuffle(points, rng);
                int use = Mathf.CeilToInt(points.Count * (bonus ? 1f : table.UseRatio));
                for (int i = 0; i < use; i++)
                {
                    var p = points[i];
                    bool high = p.position.y > 1f; // 선반 위엔 대형 금지 (docs/10)
                    // 보너스방 첫 자리는 거의 확실히 대형·특수 — 운 나쁘면 "보물방"이 평범해지지 않게
                    float bigMul = !bonus ? 1f : i == 0 ? BonusBigWeight * 100f : BonusBigWeight;
                    var item = PickLoot(table, rng, zoneIndex, bigMul, high, counts);
                    if (item == null || item.Prefab == null) continue;
                    counts[item] = (counts.TryGetValue(item, out int c) ? c : 0) + 1;
                    Spawn(item.Prefab, p.position + Vector3.up * (item.Tier == LootTier.Large ? 0.4f : 0.1f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    LootSpawned++;
                    LootValue += item.BaseValue;
                }
            }
        }

        // docs/10: 반지는 고양이 잠자리(Bed 스팟) 2m 안 고정 — 고위험 고수익의 상징. Special 전용 규칙이라 ID 하드코딩 허용
        private const string RingId = "loot_ring";

        /// <summary>이번 존 반지 위치·기준 스팟 (테스트용). 반지 없으면 null.</summary>
        public Vector3? RingPos { get; private set; }
        public Vector3 RingAnchor { get; private set; }

        public void Ring(System.Random rng)
        {
            LootItemSO ring = null;
            foreach (var e in Pop.LootTable.Entries) if (e.Item != null && e.Item.Id == RingId && e.Weight > 0) ring = e.Item;
            if (ring == null || ring.Prefab == null) return;
            var beds = new List<Vector3>();
            foreach (var room in Rooms)
                foreach (var spot in room.GetComponentsInChildren<CatSpot>())
                    if (spot.Type == CatSpotType.Bed) beds.Add(spot.transform.position);
            // 침대가 없는 존(체인이 짧게 끊긴 경우)은 고양이 시작 자리 옆 — "항상 고양이 옆"
            if (beds.Count == 0) foreach (var room in Rooms) if (room.CatSpawn != null) beds.Add(room.CatSpawn.position);
            if (beds.Count == 0) { Log.Dev("반지: 침대·고양이 자리 없음 — 이번 존엔 반지 없음"); return; }

            Vector3 anchor = beds[rng.Next(beds.Count)];
            Vector3 at = anchor;
            for (int i = 0; i < 8; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.8f + (float)rng.NextDouble() * 1.0f;
                Vector3 p = anchor + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (NavMesh.SamplePosition(p, out var hit, 0.4f, NavMesh.AllAreas) && Vector3.Distance(hit.position, anchor) <= 2f) { at = hit.position; break; }
            }
            Spawn(ring.Prefab, at + Vector3.up * 0.1f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            LootSpawned++; LootValue += ring.BaseValue;
            RingPos = at; RingAnchor = anchor;
            Log.Dev($"반지: 침대 {anchor:F1} 옆 {Vector3.Distance(at, anchor):0.0}m");
        }

        private static LootItemSO PickLoot(SpawnTableSO table, System.Random rng, int zoneIndex, float bigMul, bool high, Dictionary<LootItemSO, int> counts)
        {
            float total = 0f;
            var weights = new float[table.Entries.Length];
            for (int i = 0; i < table.Entries.Length; i++)
            {
                var e = table.Entries[i];
                if (e.Item == null || e.Weight <= 0) continue;
                if (e.Item.Id == RingId) continue; // 반지는 따로 — 고양이 침대 옆 고정 (SpawnRing)
                if (e.Max > 0 && counts.TryGetValue(e.Item, out int c) && c >= e.Max) continue;
                bool big = e.Item.Tier == LootTier.Large || e.Item.Tier == LootTier.Special;
                if (high && e.Item.Tier == LootTier.Large) continue;
                float w = e.Weight;
                if (big) w *= (1f + table.DeepZoneBonusPerIndex * zoneIndex) * bigMul; // 보너스방 = 고가치
                weights[i] = w; total += w;
            }
            if (total <= 0f) return null;
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++) { roll -= weights[i]; if (roll <= 0f && weights[i] > 0f) return table.Entries[i].Item; }
            return null;
        }

        public void Traps(System.Random rng)
        {
            var table = Pop.TrapTable;
            if (table == null || table.Entries == null || table.Entries.Length == 0) return;
            int total = 0; foreach (var e in table.Entries) total += Mathf.Max(0, e.Weight);
            foreach (var room in Rooms)
            {
                if (room == SafeRoom) continue; // 쥐구멍방엔 함정 없음 — 시작하자마자 걸리지 않게
                foreach (var p in room.TrapSpawns)
                {
                    float ratio = room == BonusRoom && BonusTrapRatio >= 0f ? BonusTrapRatio : TrapRatioOverride >= 0f ? TrapRatioOverride : table.UseRatio;
                    if (rng.NextDouble() >= ratio || total <= 0) continue;
                    int roll = rng.Next(total); GameObject prefab = null;
                    foreach (var e in table.Entries) { roll -= e.Weight; if (roll < 0) { prefab = e.Prefab; break; } }
                    if (prefab == null) continue;
                    Spawn(prefab, p.position + Vector3.up * prefab.transform.position.y, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
                    TrapsSpawned++;
                }
            }
        }

        public void HidesAndDark(System.Random rng)
        {
            int hides = 0, dark = 0;
            foreach (var room in Rooms)
            {
                if (Pop.HideSpotPrefabs != null && Pop.HideSpotPrefabs.Length > 0 && room.HideSpawns != null)
                    foreach (var p in room.HideSpawns)
                    {
                        var prefab = Pop.HideSpotPrefabs[rng.Next(Pop.HideSpotPrefabs.Length)];
                        if (prefab == null) continue;
                        Spawn(prefab, p.position + Vector3.up * prefab.transform.position.y, p.rotation);
                        hides++;
                    }
                if (Pop.DarkZonePrefab != null && room.DarkZone != null && rng.NextDouble() < Pop.DarkZoneChance)
                {
                    Spawn(Pop.DarkZonePrefab, room.DarkZone.position, room.DarkZone.rotation);
                    dark++;
                }
            }
            HidesSpawned = hides; DarkSpawned = dark;
        }

        public void Cats(System.Random rng)
        {
            if (Pop.CatPrefab == null) return;
            var candidates = new List<RoomModule>();
            foreach (var r in Rooms) if (r.CatSpawn != null) candidates.Add(r);
            Shuffle(candidates, rng);
            int n = Mathf.Min(CatCountOverride >= 0 ? CatCountOverride : Pop.CatCount, candidates.Count);
            for (int i = 0; i < n; i++)
            {
                Vector3 at = candidates[i].CatSpawn.position;
                if (NavMesh.SamplePosition(at, out var hit, 2f, NavMesh.AllAreas)) at = hit.position;
                Spawn(Pop.CatPrefab, at, candidates[i].CatSpawn.rotation);
                CatsSpawned++;
            }
        }

        public static void Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = Object.Instantiate(prefab, pos, rot);
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }
    }
}
