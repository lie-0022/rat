using RatGame.Core;
using System;
using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>상점 한 줄 — 이름·설명·레벨·구매 버튼 (uGUI). 값은 ShopPanel이 매 프레임 Refresh로 넣어준다.</summary>
    public class ShopRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private UnityEngine.UI.Button _buyButton;

        private int _index;
        private Action<int> _onBuy;

        public void Bind(int index, UpgradeSO upgrade, Action<int> onBuy)
        {
            _index = index;
            _onBuy = onBuy;
            _nameText.text = Loc.T(upgrade.DisplayName);
            _descText.text = Loc.T(upgrade.Description);
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => _onBuy?.Invoke(_index));
        }

        public void Refresh(int level, int maxLevel, int nextPrice, bool canAfford)
        {
            bool maxed = level >= maxLevel;
            SetText(_levelText, $"Lv {level}/{maxLevel}");
            SetText(_priceText, maxed ? "MAX" : $"{nextPrice}");
            _buyButton.interactable = !maxed && canAfford;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
