using RatGame.Core;
using RatGame.Player;
using RatGame.Run;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 조준점 둘레 스태미나 링 (docs/12 StaminaRing, uGUI). 소유 플레이어의 PlayerStamina를 읽기만 한다 (로컬 시뮬 값).
    /// 가득 차 있으면 숨고, 줄기 시작하면 페이드 인 — 다시 가득 차면 잠깐 뒤 페이드 아웃.
    /// 낮으면 주황, 지치면(0·헐떡임) 빨강. 메뉴 패널·결과 화면 중엔 숨긴다.
    /// </summary>
    public class StaminaRing : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private UnityEngine.UI.Image _fill;
        [SerializeField] private UnityEngine.UI.Image _track;
        [SerializeField] private Color _trackColor = new(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color _trackExhaustedColor = new(0.8f, 0.15f, 0.1f, 0.7f); // 지치면 채움이 0이라 트랙으로 알린다
        [SerializeField] private float _fadeInSpeed = 8f;    // alpha/s
        [SerializeField] private float _fadeOutSpeed = 2.5f; // alpha/s
        [SerializeField] private float _hideDelay = 0.6f;    // 가득 찬 뒤 사라지기까지 (s)
        [SerializeField, Range(0f, 1f)] private float _lowRatio = 0.25f;
        [SerializeField] private Color _normalColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color _lowColor = new(1f, 0.7f, 0.2f, 0.95f);
        [SerializeField] private Color _exhaustedColor = new(1f, 0.3f, 0.25f, 1f);

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
            if (_stamina == null || InputFocus.IsUiOpen || (run != null && run.IsShowingResult))
            {
                _group.alpha = 0f;
                return;
            }

            float max = _stamina.Max;
            float ratio = max > 0f ? Mathf.Clamp01(_stamina.Current / max) : 1f;
            // 값이 같으면 대입하지 않는다 — 매 프레임 대입하면 캔버스가 계속 다시 그려짐
            if (!Mathf.Approximately(_fill.fillAmount, ratio)) _fill.fillAmount = ratio;
            Color color = _stamina.IsExhausted ? _exhaustedColor : ratio <= _lowRatio ? _lowColor : _normalColor;
            if (_fill.color != color) _fill.color = color;
            if (_track != null)
            {
                Color trackColor = _stamina.IsExhausted ? _trackExhaustedColor : _trackColor;
                if (_track.color != trackColor) _track.color = trackColor;
            }

            if (ratio < 0.999f) _notFullAt = Time.time;
            bool show = Time.time - _notFullAt < _hideDelay;
            float speed = show ? _fadeInSpeed : _fadeOutSpeed;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, speed * Time.deltaTime);
        }
    }
}
