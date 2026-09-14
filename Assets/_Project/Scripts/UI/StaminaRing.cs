using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using RatGame.Run;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 조준점 둘레 스태미나 링 (docs/12 StaminaRing, uGUI). 소유 플레이어의 PlayerStamina를 읽기만 한다 (로컬 시뮬 값).
    /// 가득 차 있으면 숨고, 줄기 시작하면 페이드 인 — 다시 가득 차면 잠깐 뒤 페이드 아웃.
    /// 색은 테마 역할: 보통 Text, 낮으면 Warning, 지치면(0·헐떡임) 트랙이 Danger (채움이 0이라 트랙으로 알림). 메뉴 패널·결과 화면 중엔 숨긴다.
    /// </summary>
    public class StaminaRing : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private UnityEngine.UI.Image _fill;
        [SerializeField] private UnityEngine.UI.Image _track;
        [SerializeField] private float _fadeInSpeed = 8f;    // alpha/s
        [SerializeField] private float _fadeOutSpeed = 2.5f; // alpha/s
        [SerializeField] private float _hideDelay = 0.6f;    // 가득 찬 뒤 사라지기까지 (s)
        [SerializeField, Range(0f, 1f)] private float _lowRatio = 0.25f;

        private PlayerStamina _stamina;
        private float _notFullAt = float.NegativeInfinity;

        public void Bind(PlayerStamina stamina)
        {
            _stamina = stamina;
            _group.alpha = 0f;
        }

        private void Update()
        {
            var run = RunManager.Instance;
            if (_stamina == null || _theme == null || InputFocus.IsUiOpen || (run != null && run.IsShowingResult))
            {
                _group.alpha = 0f;
                return;
            }

            float max = _stamina.Max;
            float ratio = max > 0f ? Mathf.Clamp01(_stamina.Current / max) : 1f;
            // 값이 같으면 대입하지 않는다 — 매 프레임 대입하면 캔버스가 계속 다시 그려짐
            if (!Mathf.Approximately(_fill.fillAmount, ratio)) _fill.fillAmount = ratio;

            bool exhausted = _stamina.IsExhausted;
            Color fillColor = _theme.GetColor(exhausted ? UiColorRole.Danger : ratio <= _lowRatio ? UiColorRole.Warning : UiColorRole.Text);
            if (_fill.color != fillColor) _fill.color = fillColor;
            if (_track != null)
            {
                Color trackColor = _theme.GetColor(exhausted ? UiColorRole.Danger : UiColorRole.Dim);
                if (_track.color != trackColor) _track.color = trackColor;
            }

            if (ratio < 0.999f) _notFullAt = Time.time;
            bool show = Time.time - _notFullAt < _hideDelay;
            float speed = show ? _fadeInSpeed : _fadeOutSpeed;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, speed * Time.deltaTime);
        }
    }
}
