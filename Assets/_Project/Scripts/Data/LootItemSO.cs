using System;
using UnityEngine;

namespace RatGame.Data
{
    /// <summary>아이템 트레잇 (docs/05). 구현은 CarryableItem — Edible은 스태미나 생기는 태스크 1-4에서.</summary>
    [Flags]
    public enum ItemTrait
    {
        None = 0,
        Fragile = 1,    // 충돌 파손 — 깨지면 가치 0 + 소음 60
        Rolling = 2,    // 낮은 각저항 — 놓으면 굴러감, 잡기 중 각도 드라이브 끔
        Wobbly = 4,     // 조인트 damper 1/4 — 출렁임
        Slippery = 8,   // 3s마다 12% 강제 놓침
        Alarming = 16,  // 잡기/충돌 시 소음 50
        Edible = 32     // Interact 홀드로 먹기 (스태미나 회복)
    }

    /// <summary>전리품 아이템 데이터 (docs/05·08). 수치 표는 docs 표와 일치시킬 것.</summary>
    [CreateAssetMenu(fileName = "Loot_", menuName = "RatGame/Loot Item")]
    public class LootItemSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _baseValue;
        [SerializeField] private float _mass = 1f;
        [SerializeField] private ItemTrait _traits = ItemTrait.None;

        public string DisplayName => _displayName;
        public int BaseValue => _baseValue;
        public float Mass => _mass;
        public ItemTrait Traits => _traits;
        public bool Has(ItemTrait trait) => (_traits & trait) != 0;
    }
}
