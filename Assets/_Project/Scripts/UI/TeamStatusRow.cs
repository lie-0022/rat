using RatGame.Data;
using RatGame.Player;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>팀 상태 한 줄 — 팀색 견본·이름·상태 칩(기절/끈끈이!/다운, 멀쩡하면 숨김).</summary>
    public class TeamStatusRow : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private UnityEngine.UI.Image _swatch;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private UnityEngine.UI.Image _stateChip;
        [SerializeField] private TMP_Text _stateText;

        public void Show(Color color, string displayName, ConditionState state, bool grudged = false)
        {
            if (_swatch.color != color) _swatch.color = color;
            if (_nameText.text != displayName) _nameText.text = displayName;

            bool chip = state != ConditionState.Active || grudged; // 멀쩡해도 고양이에게 찍혔으면 "찍힘"
            if (_stateChip.gameObject.activeSelf != chip) _stateChip.gameObject.SetActive(chip);
            if (!chip) return;

            string label = state switch
            {
                ConditionState.Stunned => "기절",
                ConditionState.Trapped => "끈끈이!",
                ConditionState.Hidden => "숨음",
                ConditionState.Active => "찍힘",
                _ => "다운"
            };
            if (_stateText.text != label) _stateText.text = label;
            if (_theme == null) return;
            // 주황 칩 위엔 어두운 글자, 빨강 칩 위엔 흰 글자 (대비)
            bool danger = state == ConditionState.Downed || state == ConditionState.Active;
            bool calm = state == ConditionState.Hidden;
            Color bg = _theme.GetColor(danger ? UiColorRole.Danger : calm ? UiColorRole.Secondary : UiColorRole.Warning);
            Color fg = _theme.GetColor(danger || calm ? UiColorRole.Text : UiColorRole.OnAccent);
            if (_stateChip.color != bg) _stateChip.color = bg;
            if (_stateText.color != fg) _stateText.color = fg;
        }
    }
}
