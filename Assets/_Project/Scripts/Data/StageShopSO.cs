using System;
using UnityEngine;

namespace RatGame.Data
{
    [Serializable]
    public struct StageShopEntry
    {
        public string Id;
        public string DisplayName;
        public GameObject Prefab;   // 식량 가치 없는 도구만 (사서 창고에 넣는 구멍 방지 — plan cat-65)
        public int Price;           // 식량
        public string Description;
    }

    /// <summary>스테이지 사이 상점 목록 (docs/09 새 루프, 2026-09-24 고양이 65). 값은 여기서만 — 하드코딩 금지.</summary>
    [CreateAssetMenu(fileName = "StageShop", menuName = "RatGame/Stage Shop")]
    public class StageShopSO : ScriptableObject
    {
        public StageShopEntry[] Entries;

        public bool TryGet(string id, out StageShopEntry entry)
        {
            foreach (var e in Entries) if (e.Id == id) { entry = e; return true; }
            entry = default;
            return false;
        }
    }
}
