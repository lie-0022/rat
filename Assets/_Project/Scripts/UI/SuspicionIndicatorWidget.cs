using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 의심 표시 (docs/12 SuspicionIndicator, docs/07 "게이지 복제 → 타깃 HUD에 ?/!", production/plans/ui-04).
    /// 나를 쫓는 고양이(Chase && TargetClientId == 나) → 빨간 "!", 없으면 가장 가까운 의심(Suspicious) 고양이 → 주황 "?" + 게이지(임계 30→추격 100).
    /// 화면 안이면 고양이 머리 위, 화면 밖·뒤면 화면 중앙 둘레에 방향 화살표와 함께. 남을 쫓는 고양이는 표시 안 함.
    /// CatBrain·CatSenses NetworkVariable만 읽는다 (규칙 3). 대상 고르기는 0.2s마다, 위치는 매 프레임.
    /// </summary>
    public class SuspicionIndicatorWidget : MonoBehaviour
    {
        private const float PollSeconds = 0.2f;
        private const float RingRadius = 180f;      // 화면 밖일 때 중앙에서 떨어진 거리
        private const float HeadHeight = 1.3f;      // 고양이 머리 위 표시 높이 (m)
        private const float HeadOffsetPx = 36f;
        private const float ArrowDistance = 38f;    // 마커 중심에서 화살표까지
        private static readonly ScreenAnchor.Margins EdgeMargins = new(48f, 48f, 170f, 130f);

        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private RectTransform _marker;
        [SerializeField] private UnityEngine.UI.Image _ring;
        [SerializeField] private TMP_Text _symbol;
        [SerializeField] private RectTransform _arrow;
        [SerializeField] private GameObject _gauge;
        [SerializeField] private RectTransform _gaugeFill;

        private RectTransform _canvasRect;
        private string _attentionGlyph;
        // 놀고 있음 표시. Jua(한글 폰트)엔 ♪가 없어 "냥" — 원 안에 한 글자로 읽히고 필러 4(귀여움)에도 맞는다
        private string AttentionGlyph => _attentionGlyph ??= _symbol.font != null && _symbol.font.HasCharacter('\u266A') ? "\u266A" : "냥";
        private CatBrain _cat;
        private bool _chasingMe;
        private bool _grudgedMe; // 앙심 대상이면 "!!" (design/cat-ideas/05)
        private bool _attending; // 놀고 있다 — 유인이 먹히는 중 "♪" (design/cat-ideas/13)
        private float _nextPoll;
        private string _lastLogged = "";

        public bool IsShowing => _marker.gameObject.activeSelf;
        public string Symbol => _symbol.text;

        private void Awake()
        {
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            _marker.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollSeconds;
                PickTarget();
            }

            var cam = Camera.main;
            bool show = _cat != null && cam != null;
            if (_marker.gameObject.activeSelf != show) _marker.gameObject.SetActive(show);
            if (!show)
            {
                if (_lastLogged.Length > 0) { _lastLogged = ""; Log.Dev("의심 표시: 없음"); }
                return;
            }

            Refresh(cam);
        }

        private void PickTarget()
        {
            _cat = null;
            _chasingMe = false;
            _grudgedMe = false;
            _attending = false;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
            if (RunManager.Instance != null && RunManager.Instance.IsShowingResult) return;

            ulong me = nm.LocalClientId;
            Vector3 myPos = nm.LocalClient.PlayerObject.transform.position;
            float nearest = float.PositiveInfinity;
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                var state = cat.State.Value;
                if (state == CatState.Chase && cat.TargetClientId.Value == me)
                {
                    _cat = cat;
                    _chasingMe = true;
                    _grudgedMe = cat.HasGrudge.Value && cat.GrudgeClientId.Value == me;
                    return; // 나를 쫓는 고양이가 최우선
                }
                if (state != CatState.Suspicious) continue;
                float d = (cat.transform.position - myPos).sqrMagnitude;
                if (d < nearest) { nearest = d; _cat = cat; }
            }
            if (_cat != null) return;
            // 의심하는 고양이가 없으면: 가까이서 노는(호기심·유인) 고양이 "♪" — 지금이 기회라는 신호
            float range = _balance != null ? _balance.AttentionIndicatorRange : 15f;
            nearest = range * range;
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                var state = cat.State.Value;
                if (state != CatState.Curious && state != CatState.Distracted) continue;
                float d = (cat.transform.position - myPos).sqrMagnitude;
                if (d < nearest) { nearest = d; _cat = cat; _attending = true; }
            }
        }

        private void Refresh(Camera cam)
        {
            Vector3 head = _cat.transform.position + Vector3.up * HeadHeight;
            Vector2 p = ScreenAnchor.ToCanvas(cam, _canvasRect, head, EdgeMargins, out bool onScreen);

            if (onScreen)
            {
                _marker.anchoredPosition = p + Vector2.up * HeadOffsetPx;
                if (_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(false);
            }
            else
            {
                // 가장자리 방향만 쓰고 중앙 둘레에 둔다 — 조준점 근처라 곁눈으로도 읽힌다
                Vector2 dir = p.sqrMagnitude > 1f ? p.normalized : Vector2.down;
                _marker.anchoredPosition = dir * RingRadius;
                if (!_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(true);
                _arrow.anchoredPosition = dir * ArrowDistance;
                // 흰 정사각형의 꼭짓점 하나가 dir을 가리키게 (모서리는 회전각 +45°에 있다)
                _arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 45f);
            }

            string symbol = _chasingMe ? (_grudgedMe ? "!!" : "!") : _attending ? AttentionGlyph : "?";
            if (_lastLogged != symbol) { _lastLogged = symbol; Log.Dev($"의심 표시: {symbol} ({_cat.name} {_cat.State.Value})"); }
            if (_symbol.text != symbol) _symbol.text = symbol;
            if (_theme != null)
            {
                Color c = _theme.GetColor(_chasingMe ? UiColorRole.Danger : _attending ? UiColorRole.Positive : UiColorRole.Warning);
                if (_ring.color != c) _ring.color = c;
                var arrowImage = _arrow.GetComponent<UnityEngine.UI.Image>();
                if (arrowImage.color != c) arrowImage.color = c;
            }

            bool gauge = !_chasingMe && !_attending;
            if (_gauge.activeSelf != gauge) _gauge.SetActive(gauge);
            if (gauge)
            {
                var senses = _cat.GetComponent<CatSenses>();
                float max = _balance != null ? _balance.CatChaseThreshold : 100f;
                float t = senses != null ? Mathf.Clamp01(senses.SuspicionGauge.Value / max) : 0f;
                if (!Mathf.Approximately(_gaugeFill.anchorMax.x, t)) _gaugeFill.anchorMax = new Vector2(t, 1f);
            }
        }
    }
}
