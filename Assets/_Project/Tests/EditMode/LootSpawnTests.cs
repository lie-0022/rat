using System.Collections.Generic;
using NUnit.Framework;
using RatGame.Data;
using RatGame.Run;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 전리품이 바닥에 묻힌 채 나지 않는다 (고양이 341) — 띄우는 높이(ZonePopulator.SpawnLift)가 실제 콜라이더 밑면보다 커야.
    /// 전엔 대형 0.4m·나머지 0.1m 고정이라 통닭은 41cm, 납작한 접시(캡슐)는 19cm 묻혀 났다가 물리가 튕겨 냈다.
    /// </summary>
    public class LootSpawnTests
    {
        [Test]
        public void 전리품_띄우는_높이가_콜라이더_밑면보다_큼()
        {
            var bad = new List<string>();
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:LootItemSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<LootItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.Prefab == null) continue;
                float lift = ZonePopulator.SpawnLift(item.Prefab, item.Tier == LootTier.Large ? 0.4f : 0.1f);
                var go = Object.Instantiate(item.Prefab, Vector3.zero, Quaternion.identity);
                try
                {
                    Physics.SyncTransforms();
                    float bottom = float.MaxValue;
                    foreach (var c in go.GetComponentsInChildren<Collider>())
                        if (!c.isTrigger) bottom = Mathf.Min(bottom, c.bounds.min.y);
                    if (bottom == float.MaxValue) continue;
                    count++;
                    if (lift + bottom < -0.005f) bad.Add($"{item.name}: 띄움 {lift:0.00} < 밑면 깊이 {-bottom:0.00}");
                }
                finally { Object.DestroyImmediate(go); }
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
            Assert.Greater(count, 15);
        }
    }
}
