using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>도감 한 줄 — 이름·가치·한 줄 설명. 해금은 Secondary 배경, 미해금은 "???"와 Row 배경 (테마 역할).</summary>
    public class CodexRow : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private UnityEngine.UI.Image _background;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private TMP_Text _flavorText;

        public void Set(LootItemSO item, bool unlocked)
        {
            if (_theme != null) _background.color = _theme.GetColor(unlocked ? UiColorRole.Secondary : UiColorRole.Row);
            _nameText.text = unlocked ? item.DisplayName : "???";
            _valueText.text = unlocked ? $"{item.BaseValue}" : "-";
            _flavorText.text = unlocked ? item.CodexFlavor : "아직 못 가져온 물건";
        }
    }
}
