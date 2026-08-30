using System;
using UnityEngine;

namespace RatGame.Data
{
    /// <summary>아이템 트레잇 (docs/05). 구현은 CarryableItem — Edible은 스태미나 회복(치즈류).</summary>
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

    public enum LootTier { Small, Tricky, Large, Special }

    /// <summary>전리품 데이터 원장 (docs/08). Id는 저장·도감 키 — 절대 변경 금지.</summary>
    [CreateAssetMenu(fileName = "Loot_", menuName = "RatGame/Loot Item")]
    public class LootItemSO : ScriptableObject
    {
        [SerializeField] private string _id;            // "loot_egg"
        [SerializeField] private string _displayName;   // "계란"
        [SerializeField, TextArea] private string _codexFlavor; // 댕청한 도감 한 줄 (B급)
        [SerializeField] private int _baseValue;
        [SerializeField] private float _mass = 1f;
        [SerializeField] private ItemTrait _traits = ItemTrait.None;
        [SerializeField] private LootTier _tier = LootTier.Small;
        [SerializeField] private GameObject _prefab;    // CarryableItem 프리팹
        [SerializeField] private Sprite _icon;          // 도감·HUD (아트 단계)

        public string Id => _id;
        public string DisplayName => _displayName;
        public string CodexFlavor => _codexFlavor;
        public int BaseValue => _baseValue;
        public float Mass => _mass;
        public ItemTrait Traits => _traits;
        public LootTier Tier => _tier;
        public GameObject Prefab => _prefab;
        public Sprite Icon => _icon;
        public bool Has(ItemTrait trait) => (_traits & trait) != 0;
    }
}
