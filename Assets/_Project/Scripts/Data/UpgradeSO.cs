using UnityEngine;

namespace RatGame.Data
{
    /// <summary>상점 업그레이드 효과 종류 — 효과당 업그레이드 1개, 수치는 BalanceConfigSO (docs/11 상점).</summary>
    public enum UpgradeEffect { CarrySlots, MoveSpeed, StaminaMax, ThrowPower, JumpPower }

    /// <summary>
    /// 기지 상점 업그레이드 항목 (docs/11). 팀 누계(HaulTotal)로 산다 — 레포식이라 팀 공유·호스트 저장.
    /// 레벨당 효과 수치는 하드코딩 금지 규칙대로 BalanceConfigSO에 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade_", menuName = "RatGame/Upgrade")]
    public class UpgradeSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private UpgradeEffect _effect;
        [SerializeField, Min(1)] private int _maxLevel = 3;
        [SerializeField, Min(0)] private int _basePrice = 100;   // 1레벨 가격
        [SerializeField, Min(0)] private int _priceStep = 100;   // 레벨마다 더해지는 가격

        public string DisplayName => _displayName;
        public string Description => _description;
        public UpgradeEffect Effect => _effect;
        public int MaxLevel => _maxLevel;

        /// <summary>다음 레벨(1부터) 가격.</summary>
        public int PriceForLevel(int nextLevel) => _basePrice + _priceStep * Mathf.Max(0, nextLevel - 1);
    }
}
