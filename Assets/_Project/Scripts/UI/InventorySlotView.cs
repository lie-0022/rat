using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>인벤 슬롯 한 칸 — 선택이면 테두리(Accent)가 보인다. 값이 바뀔 때만 대입 (캔버스 리빌드 방지).</summary>
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private UnityEngine.UI.Image _frame; // 선택 테두리
        [SerializeField] private TMP_Text _label;

        private int _number = -1;
        private string _text;
        private bool? _selected;

        public void Set(int number, string itemName, bool selected)
        {
            if (number != _number || itemName != _text)
            {
                _number = number;
                _text = itemName;
                _label.text = $"{number}  {itemName}";
            }
            if (_selected != selected)
            {
                _selected = selected;
                if (_theme != null) _frame.color = selected ? _theme.GetColor(UiColorRole.Accent) : Color.clear;
            }
        }
    }
}
