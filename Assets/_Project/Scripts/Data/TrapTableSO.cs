using System;
using UnityEngine;

namespace RatGame.Data
{
    [Serializable] public struct TrapEntry { public GameObject Prefab; public int Weight; }

    /// <summary>함정 가중치 테이블 (docs/10, 2026-09-24 고양이 58). 함정 지점의 UseRatio만 채운다.</summary>
    [CreateAssetMenu(fileName = "TrapTable", menuName = "RatGame/Trap Table")]
    public class TrapTableSO : ScriptableObject
    {
        public TrapEntry[] Entries;
        [Range(0f, 1f)] public float UseRatio = 0.5f;           // docs/10 "50% 사용"
    }
}
