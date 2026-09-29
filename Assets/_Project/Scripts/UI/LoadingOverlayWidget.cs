using RatGame.Core;
using RatGame.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.UI
{
    /// <summary>
    /// 씬 전환 로딩 오버레이 (docs/12 화면 플로우 "로딩(팁 문구)", production/plans/ui-01). Hud 안 — Hud가 DontDestroyOnLoad라 전환을 살아남는다.
    /// 내 클라가 Single 로드를 시작하면(SceneEventType.Load) 켜고, 전원 로드 완료(LoadEventCompleted)에 페이드 아웃 —
    /// 스테이지는 그 순간 순간이동·출발하므로 남이 로딩 중인 동안 멈춘 화면이 보이지 않게. NGO는 진행률을 주지 않아 진행 바는 없다.
    /// </summary>
    public class LoadingOverlayWidget : MonoBehaviour
    {
        private const string BaseSceneName = "Hub";
        private const float MaxSeconds = 15f;   // 전원 완료 이벤트를 못 받아도 화면이 영영 가려지지 않게
        private const float FadeSeconds = 0.4f;
        private const float DotSeconds = 0.4f;

        [SerializeField] private LoadingTipsSO _tips;
        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _tip;
        [SerializeField] private TMP_Text _dots;
        [SerializeField] private TMP_Text _waiting;

        private NetworkSceneManager _sceneManager;
        private bool _open;
        private bool _fading;
        private float _openedAt;
        private float _fadeStartedAt;
        private int _tipIndex = -1;

        public bool IsOpen => _open;

        private void Awake() => _panel.gameObject.SetActive(false);

        private void OnDestroy() => Subscribe(null);

        private void Update()
        {
            // SceneManager는 세션마다 새로 생기므로 바뀌면 다시 구독
            var nm = NetworkManager.Singleton;
            var sm = nm != null ? nm.SceneManager : null;
            if (sm != _sceneManager) Subscribe(sm);

            if (!_panel.gameObject.activeSelf) return;
            float now = Time.unscaledTime;
            if (_open)
            {
                if (now - _openedAt > MaxSeconds) Hide("시간 초과");
                int dots = 1 + (int)((now - _openedAt) / DotSeconds) % 3;
                string text = new string('.', dots);
                if (_dots.text != text) _dots.text = text;
            }
            if (_fading)
            {
                float alpha = 1f - (now - _fadeStartedAt) / FadeSeconds;
                if (alpha <= 0f)
                {
                    _fading = false;
                    _panel.gameObject.SetActive(false);
                }
                else _panel.alpha = alpha;
            }
        }

        private void Subscribe(NetworkSceneManager sm)
        {
            if (_sceneManager != null) _sceneManager.OnSceneEvent -= OnSceneEvent;
            _sceneManager = sm;
            if (_sceneManager != null) _sceneManager.OnSceneEvent += OnSceneEvent;
        }

        private void OnSceneEvent(SceneEvent e)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            switch (e.SceneEventType)
            {
                case SceneEventType.Load:
                    if (e.ClientId == nm.LocalClientId && e.LoadSceneMode == LoadSceneMode.Single) Show(e.SceneName);
                    break;
                case SceneEventType.LoadComplete:
                    if (_open && e.ClientId == nm.LocalClientId) _waiting.gameObject.SetActive(true);
                    break;
                case SceneEventType.LoadEventCompleted:
                    if (_open) Hide("전원 로드 완료");
                    break;
            }
        }

        private void Show(string sceneName)
        {
            _open = true;
            _fading = false;
            _openedAt = Time.unscaledTime;
            _title.text = Loc.T(sceneName == BaseSceneName ? "기지로 돌아가는 중" : "창고로 출발");
            if (_tips != null)
            {
                _tipIndex = _tips.PickIndex(_tipIndex);
                _tip.text = _tips.Get(_tipIndex);
            }
            _dots.text = ".";
            _waiting.gameObject.SetActive(false);
            _panel.alpha = 1f;
            _panel.gameObject.SetActive(true);
            Log.Dev($"로딩 화면 켬 → {sceneName}");
        }

        private void Hide(string reason)
        {
            _open = false;
            _fading = true;
            _fadeStartedAt = Time.unscaledTime;
            Log.Dev($"로딩 화면 끔 ({reason}, {Time.unscaledTime - _openedAt:0.00}s)");
        }
    }
}
