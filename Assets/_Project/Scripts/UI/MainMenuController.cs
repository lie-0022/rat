using RatGame.Core;
using RatGame.Data;
using RatGame.Net;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.UI
{
    /// <summary>
    /// 메인 메뉴 (docs/12 와이어프레임, uGUI). 호스트 시작 → 기지 로드 / 친구 방 참가(Steam 로비 전까지 로컬 127.0.0.1) /
    /// 설정(다음 단계에서 공용 패널 연결) / 종료. 네트워크 시작은 NetworkLauncher에 요청만 한다.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private string _hubSceneName = "Hub";
        [SerializeField] private UnityEngine.UI.Button _hostButton;
        [SerializeField] private UnityEngine.UI.Button _joinButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _quitButton;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private TMP_Text _modeText;
        [SerializeField] private SettingsPanel _settingsPanel;

        private bool _joining;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _hostButton.onClick.AddListener(OnHost);
            _joinButton.onClick.AddListener(OnJoin);
            _settingsButton.onClick.AddListener(OnSettings);
            _quitButton.onClick.AddListener(OnQuit);
            _versionText.text = $"v{Application.version}{(Debug.isDebugBuild ? " · 개발 빌드" : "")}";
            _modeText.text = "로컬 모드 (Steam 연결 전)";
            SetStatus("", UiColorRole.TextMuted);
        }

        private void Start()
        {
            // 세션을 나와 메뉴로 돌아온 경우 커서가 잠겨 있을 수 있다
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            string exitReason = NetworkLauncher.Instance != null ? NetworkLauncher.Instance.ConsumeExitReason() : null;
            if (!string.IsNullOrEmpty(exitReason)) SetStatus(exitReason, UiColorRole.DangerText);
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private async void OnHost()
        {
            SetBusy(true);
            SetStatus("기지를 여는 중…", UiColorRole.TextMuted);
            bool ok = await NetworkLauncher.Instance.StartHostAsync();
            if (!ok)
            {
                SetBusy(false);
                SetStatus("호스트를 시작하지 못했어요. 이미 켜진 게임이 있는지 확인하세요.", UiColorRole.DangerText);
                return;
            }
            NetworkManager.Singleton.SceneManager.LoadScene(_hubSceneName, LoadSceneMode.Single);
        }

        private async void OnJoin()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            SetBusy(true);
            SetStatus("기지에 접속하는 중…", UiColorRole.TextMuted);
            _joining = true;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            bool ok = await NetworkLauncher.Instance.JoinAsync();
            if (!ok) FailJoin("접속을 시작하지 못했어요.");
            // 성공하면 호스트가 기지 씬을 동기화해 이 씬이 내려간다
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (!_joining || nm == null || (clientId != nm.LocalClientId && clientId != 0)) return;
            // 승인 거절(정원 초과 등)은 우리가 쓴 사유 그대로, 트랜스포트 내부 사유("[Disconnect Event]…")는 읽을 수 있는 문구로
            string reason = nm.DisconnectReason;
            if (string.IsNullOrEmpty(reason) || reason.StartsWith("["))
                reason = "호스트를 찾지 못했어요. 친구가 방을 열었는지 확인하세요.";
            FailJoin(reason);
        }

        private void FailJoin(string message)
        {
            _joining = false;
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
                if (nm.IsListening) nm.Shutdown();
            }
            // JoinAsync가 Lobby로 넘겼으므로 메뉴 상태로 되돌린다 (다음 호스트 시작의 전이 오류 방지)
            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Current == GameState.Lobby)
                GameStateMachine.Instance.TransitionTo(GameState.MainMenu);
            SetBusy(false);
            SetStatus(message, UiColorRole.DangerText);
        }

        private void OnSettings() => _settingsPanel.Open();

        private void OnQuit()
        {
            Log.Dev("메인 메뉴: 종료");
            Application.Quit();
        }

        private void SetBusy(bool busy)
        {
            _hostButton.interactable = !busy;
            _joinButton.interactable = !busy;
        }

        private void SetStatus(string message, UiColorRole role)
        {
            _statusText.text = message;
            if (_theme != null) _statusText.color = _theme.GetColor(role);
        }
    }
}
