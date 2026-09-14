using RatGame.Data;
using RatGame.Player;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 던지기 차지 게이지 — 조준점 오른쪽 고정, 아래서 위로 참 (1인칭이라 머리 월드 위치 투영 대신 화면 고정).
    /// 색은 차지에 따라 Warning → Danger (테마 역할).
    /// </summary>
    public class ThrowGaugeWidget : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private GameObject _root;
        [SerializeField] private UnityEngine.UI.Image _fill; // Filled / Vertical / Bottom

        private PlayerCarryController _carry;

        public void Bind(PlayerCarryController carry) => _carry = carry;

        private void Update()
        {
            float charge = _carry != null ? _carry.ThrowCharge : 0f;
            bool show = charge > 0f;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            if (!Mathf.Approximately(_fill.fillAmount, charge)) _fill.fillAmount = charge;
            if (_theme != null)
            {
                Color danger = _theme.GetColor(UiColorRole.Danger);
                danger.a = 1f;
                _fill.color = Color.Lerp(_theme.GetColor(UiColorRole.Warning), danger, charge);
            }
        }
    }
}
