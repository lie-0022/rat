using System.Collections.Generic;
using RatGame.World;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 통째 시험: 스테이지가 열리면 물건이 벽·가구에 끼어 났는지 센다 (고양이 341). 통째 시험은 물건을 순간이동으로 옮겨서
    /// 끼인 물건을 못 잡는 판을 볼 수 없었다. 몸통 콜라이더가 정적 지형(RoomStatic)에 5cm 넘게 파묻히면 "끼임".
    /// </summary>
    public static partial class SoakHarness
    {
        private const float EmbedDepth = 0.05f;
        private const float SunkDepth = 0.1f;
        private static int _lootChecked, _lootEmbedded, _lootSunk;
        private static readonly List<string> EmbeddedSamples = new();
        private static readonly Dictionary<string, float> SunkByItem = new(); // 이름별 가장 깊이
        private static readonly Dictionary<string, string> SunkDetail = new(); // 이름별 그때 상태 (원인 찾기)

        private static void CheckEmbeddedLoot()
        {
            if (_stagesSeen <= 1) { _lootChecked = 0; _lootEmbedded = 0; _lootSunk = 0; EmbeddedSamples.Clear(); SunkByItem.Clear(); SunkDetail.Clear(); }
            int staticMask = LayerMask.GetMask("RoomStatic");
            var hits = new Collider[16];
            foreach (var item in Object.FindObjectsByType<CarryableItem>(FindObjectsSortMode.None))
            {
                _lootChecked++;
                bool embedded = false, sunk = false;
                foreach (var col in item.GetComponentsInChildren<Collider>())
                {
                    if (col.isTrigger || !col.enabled) continue;
                    int n = Physics.OverlapBoxNonAlloc(col.bounds.center, col.bounds.extents, hits, Quaternion.identity, staticMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        if (!Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, hits[i], hits[i].transform.position, hits[i].transform.rotation, out Vector3 dir, out float depth)) continue;
                        // 위아래로 밀어내는 겹침 = 바닥에 닿음(살짝 겹치는 건 정상) — 깊으면 "가라앉음"만 따로. 옆으로 밀어내는 겹침 = 벽·가구에 끼임
                        if (Mathf.Abs(dir.y) > 0.7f)
                        {
                            if (depth <= SunkDepth) continue;
                            sunk = true;
                            string key = item.name.Replace("(Clone)", "");
                            if (!SunkByItem.TryGetValue(key, out float d) || depth > d)
                            {
                                SunkByItem[key] = depth;
                                var rb = item.GetComponent<Rigidbody>();
                                SunkDetail[key] = $"아래 {col.bounds.min.y:0.00}·바닥 {hits[i].bounds.max.y:0.00}·{hits[i].name}·키네마틱 {(rb != null && rb.isKinematic)}·잠 {(rb != null && rb.IsSleeping())}·크기 {item.transform.lossyScale.x:0.0}";
                            }
                        }
                        else if (depth > EmbedDepth) embedded = true;
                    }
                }
                if (sunk) _lootSunk++;
                if (!embedded) continue;
                _lootEmbedded++;
                if (EmbeddedSamples.Count < 3) EmbeddedSamples.Add($"S{_stagesSeen} {item.name} {item.transform.position:F1}");
            }
        }

        private static string EmbeddedReport() =>
            $"끼인 물건 {_lootEmbedded}/{_lootChecked} · 바닥에 가라앉은 것 {_lootSunk}" +
            (SunkByItem.Count > 0 ? " [" + string.Join(", ", System.Linq.Enumerable.Select(SunkByItem, kv => $"{kv.Key} {kv.Value:0.00}m ({SunkDetail[kv.Key]})")) + "]" : "") + (EmbeddedSamples.Count > 0 ? " (" + string.Join(", ", EmbeddedSamples) + ")" : "");
    }
}
