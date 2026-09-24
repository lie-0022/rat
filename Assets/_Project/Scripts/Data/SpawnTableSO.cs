using System;
using UnityEngine;

namespace RatGame.Data
{
    [Serializable] public struct SpawnEntry { public LootItemSO Item; public int Weight; public int Max; }

    /// <summary>전리품 가중치 테이블 (docs/10, 2026-09-24 고양이 58). 스폰 지점의 UseRatio만 채운다.</summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "RatGame/Spawn Table")]
    public class SpawnTableSO : ScriptableObject
    {
        public SpawnEntry[] Entries;
        [Range(0f, 1f)] public float UseRatio = 0.7f;           // docs/10 "스폰 지점의 70%만 사용"
        public float DeepZoneBonusPerIndex = 0.3f;              // Large/Special 가중치 × (1 + 0.3 × zoneIndex)
    }
}
