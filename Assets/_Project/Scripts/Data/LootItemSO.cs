using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 전리품 아이템 데이터. 상세 필드(트레잇·파손 등)는 docs/08-loot-and-economy.md 표 확정 시 확장 (태스크 1-7).
    /// </summary>
    [CreateAssetMenu(fileName = "Loot_", menuName = "RatGame/Loot Item")]
    public class LootItemSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _baseValue;
        [SerializeField] private float _mass = 1f;

        public string DisplayName => _displayName;
        public int BaseValue => _baseValue;
        public float Mass => _mass;
    }
}
