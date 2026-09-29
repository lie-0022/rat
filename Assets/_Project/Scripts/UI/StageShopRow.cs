using RatGame.Core;
using System;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>목적지 상점 한 줄 — 이름·설명·값·사기 (고양이 65).</summary>
    public class StageShopRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private UnityEngine.UI.Button _buyButton;

        public void Bind(string displayName, string description, int price, Action onBuy)
        {
            _nameText.text = displayName;
            _descText.text = description;
            _priceText.text = Loc.F("{0} 식량", price);
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => onBuy?.Invoke());
        }

        public void Refresh(bool affordable) { if (_buyButton.interactable != affordable) _buyButton.interactable = affordable; }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text name, TMP_Text desc, TMP_Text price, UnityEngine.UI.Button buy) { _nameText = name; _descText = desc; _priceText = price; _buyButton = buy; }
#endif
    }
}
