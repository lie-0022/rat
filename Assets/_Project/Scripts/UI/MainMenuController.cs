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
    /// 메인 메뉴 (docs/12 와이어프레임, uGUI). 호스트 시작 → 기지 로드 / 친구 방 참가 / 설정(공용 패널) / 종료.
    /// 친구 방 참가: Steam 모드는 친구 목록을 열고 초대 수락을 기다린다(수락하면 NetworkLauncher.JoinStarted), 로컬 모드는 127.0.0.1.
    /// 네트워크 시작은 NetworkLauncher에 요청만 하고, 실패 사유는 LaunchFailed 이벤트로 받는다.
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
        private string _lastFailure;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _hostButton.onClick.AddListener(OnHost);
            _joinButton.onClick.AddListener(OnJoin);
            _settingsButton.onClick.AddListener(OnSettings);
            _quitButton.onClick.AddListener(OnQuit);
            _versionText.text = $"v{Application.version}{(Debug.isDebugBuild ? Loc.T(" · 개발 빌드") : "")}";
            var launcher = NetworkLauncher.Instance;
            _modeText.text = launcher != null && launcher.UsingSteam
                ? $"Steam · {launcher.Steam.PersonaName}"
                : Loc.T("로컬 모드 (같은 PC에서만 참가)");
            SetStatus("", UiColorRole.TextMuted);
            if (launcher != null)
            {
                launcher.JoinStarted += OnInviteJoinStarted;
                launcher.LaunchFailed += OnLaunchFailed;
            }
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
            var launcher = NetworkLauncher.Instance;
            if (launcher != null)
            {
                launcher.JoinStarted -= OnInviteJoinStarted;
                launcher.LaunchFailed -= OnLaunchFailed;
            }
        }

        private void OnLaunchFailed(string message)
        {
            _lastFailure = message;
            if (_joining) FailJoin(message);
        }

        private async void OnHost()
        {
            SetBusy(true);
            SetStatus(Loc.T("기지를 여는 중…"), UiColorRole.TextMuted);
            _lastFailure = null;
            bool ok = await NetworkLauncher.Instance.StartHostAsync();
            if (!ok)
            {
                SetBusy(false);
                SetStatus(_lastFailure ?? Loc.T("호스트를 시작하지 못했어요. 이미 켜진 게임이 있는지 확인하세요."), UiColorRole.DangerText);
                return;
            }
            NetworkManager.Singleton.SceneManager.LoadScene(_hubSceneName, LoadSceneMode.Single);
        }

        private async void OnJoin()
        {
            var launcher = NetworkLauncher.Instance;
            if (launcher != null && launcher.UsingSteam)
            {
                // Steam은 초대로만 들어간다 — 친구 목록을 열어 두고 초대 수락(또는 친구의 "게임 참가")을 기다린다
                launcher.Steam.OpenFriendsOverlay();
                SetStatus(Loc.T("친구가 보낸 초대를 수락하면 바로 들어가요.\n(Steam 친구 목록에서 \"게임 참가\"도 돼요)"), UiColorRole.TextMuted);
                return;
            }
            BeginJoinUi(Loc.T("기지에 접속하는 중…"));
            bool ok = await launcher.JoinAsync();
            if (!ok && _joining) FailJoin(Loc.T("접속을 시작하지 못했어요."));
            // 성공하면 호스트가 기지 씬을 동기화해 이 씬이 내려간다
        }

        private void OnInviteJoinStarted(string friendName) => BeginJoinUi($"{friendName}의 방에 접속하는 중…");

        private void BeginJoinUi(string status)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            SetBusy(true);
            SetStatus(status, UiColorRole.TextMuted);
            _joining = true;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (!_joining || nm == null || (clientId != nm.LocalClientId && clientId != 0)) return;
            // 승인 거절(정원 초과 등)은 우리가 쓴 사유 그대로, 트랜스포트 내부 사유("[Disconnect Event]…")는 읽을 수 있는 문구로
            string reason = nm.DisconnectReason;
            if (string.IsNullOrEmpty(reason) || reason.StartsWith("["))
                reason = Loc.T("호스트를 찾지 못했어요. 친구가 방을 열었는지 확인하세요.");
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
