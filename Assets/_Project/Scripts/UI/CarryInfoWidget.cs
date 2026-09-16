using RatGame.Data;
using RatGame.Player;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 손에 든 물건 카드 (docs/12 HUD CarryInfo, production/plans/ui-02). 인벤 슬롯 바로 위.
    /// 아이콘(있을 때만)·이름·가치(금 가면 "금 감")·무게 칩: 대형 = "무거움! 같이 들자 (n/m)"(자리 다 차면 "함께 드는 중"),
    /// 1인당 하중이 달리기/점프 차단 이상 = "무거움 — 못 달림(·못 뜀)".
    /// PlayerCarryController·CarryableItem 상태만 읽는다 (규칙 3). 결과 화면 중엔 슬롯 그룹과 함께 숨는다.
    /// </summary>
    public class CarryInfoWidget : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private GameObject _card;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private GameObject _crackedTag;
        [SerializeField] private UnityEngine.UI.Image _chip;
        [SerializeField] private TMP_Text _chipText;

        private PlayerCarryController _carry;

        public void Bind(PlayerCarryController carry) => _carry = carry;

        private void Update()
        {
            var item = _carry != null && _carry.IsHolding ? _carry.CarriedItem : null;
            bool show = item != null;
            if (_card.activeSelf != show) _card.SetActive(show);
            if (!show) return;

            var data = item.Data;
            var icon = data != null ? data.Icon : null;
            // 아이콘이 없으면 빈 사각형 대신 자리째 숨긴다 (스프라이트는 아트 단계)
            if (_icon.gameObject.activeSelf != (icon != null)) _icon.gameObject.SetActive(icon != null);
            if (icon != null && _icon.sprite != icon) _icon.sprite = icon;

            SetText(_nameText, data != null ? data.DisplayName : item.name);
            SetText(_valueText, $"가치 {item.EffectiveValue}");
            bool cracked = item.IsCracked;
            if (_crackedTag.activeSelf != cracked) _crackedTag.SetActive(cracked);
            SetColor(_valueText, cracked ? UiColorRole.DangerText : UiColorRole.AccentText);

            RefreshChip(item);
        }

        private void RefreshChip(World.CarryableItem item)
        {
            string label = null;
            UiColorRole bg = UiColorRole.Warning, fg = UiColorRole.OnAccent;
            if (item.IsHeavy)
            {
                // 자리가 다 찼으면 더 부를 사람이 없다 — 재촉 대신 상태만
                bool full = item.IsCarrySlotsFull;
                label = full
                    ? $"무거움 — 함께 드는 중 ({item.CarrierIds.Count}/{item.CarrySlotCount})"
                    : $"무거움! 같이 들자 ({item.CarrierIds.Count}/{item.CarrySlotCount})";
                if (!full)
                {
                    bg = UiColorRole.Danger;
                    fg = UiColorRole.Text;
                }
            }
            else if (_balance != null)
            {
                float load = _carry.CurrentLoadPerRat;
                if (load >= _balance.JumpBlockLoad) label = "무거움 — 못 달림·못 뜀";
                else if (load >= _balance.SprintBlockLoad) label = "무거움 — 못 달림";
            }

            bool chip = label != null;
            if (_chip.gameObject.activeSelf != chip) _chip.gameObject.SetActive(chip);
            if (!chip) return;
            SetText(_chipText, label);
            SetColor(_chip, bg);
            SetColor(_chipText, fg);
        }

        private void SetColor(UnityEngine.UI.Graphic graphic, UiColorRole role)
        {
            if (_theme == null) return;
            Color color = _theme.GetColor(role);
            if (graphic.color != color) graphic.color = color;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
