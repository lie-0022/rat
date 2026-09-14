using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>도감 한 줄 — 이름·가치·한 줄 설명. 미해금은 "???"와 흐린 배경.</summary>
    public class CodexRow : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _background;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private TMP_Text _flavorText;
        [SerializeField] private Color _unlockedColor = new(0.2f, 0.18f, 0.12f, 0.8f);
        [SerializeField] private Color _lockedColor = new(0f, 0f, 0f, 0.3f);

        public void Set(LootItemSO item, bool unlocked)
        {
            _background.color = unlocked ? _unlockedColor : _lockedColor;
            _nameText.text = unlocked ? item.DisplayName : "???";
            _valueText.text = unlocked ? $"{item.BaseValue}" : "-";
            _flavorText.text = unlocked ? item.CodexFlavor : "아직 못 가져온 물건";
        }
    }
}
