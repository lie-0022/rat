using RatGame.Core;
using RatGame.Net;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 일시정지 창 (docs/12, Hud 안). Esc로 열고 게임은 계속 돈다(멀티). 계속하기 / 설정(공용 패널) / 세션 나가기(확인 후 메인 메뉴).
    /// 열려 있는 동안 InputFocus로 커서 해제·조작 정지. 다른 메뉴 패널이 열려 있으면 그 패널이 Esc를 먼저 쓴다.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private GameObject _mainWindow;
        [SerializeField] private GameObject _confirmWindow;
        [SerializeField] private TMP_Text _sessionText;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private UnityEngine.UI.Button _resumeButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _leaveButton;
        [SerializeField] private UnityEngine.UI.Button _confirmLeaveButton;
        [SerializeField] private UnityEngine.UI.Button _cancelLeaveButton;
        [SerializeField] private SettingsPanel _settings;

        private bool _open;
        private float _openedAt;

        public bool IsOpen => _open;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _resumeButton.onClick.AddListener(() => Close(false));
            _settingsButton.onClick.AddListener(OpenSettings);
            _leaveButton.onClick.AddListener(ShowConfirm);
            _confirmLeaveButton.onClick.AddListener(LeaveSession);
            _cancelLeaveButton.onClick.AddListener(HideConfirm);
            _settings.Closed += OnSettingsClosed;
            Loc.Changed += OnLanguageChanged;
            _root.SetActive(false);
        }

        // 설정 창에서 언어를 바꾸고 돌아오면 세션 줄도 새 언어로
        private void OnLanguageChanged() => _sessionText.text = DescribeSession();

        // 세션 종료로 HUD째 파괴돼도 입력이 묶여 있지 않게
        private void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            if (_open) InputFocus.PanelClosed(false);
        }

        private void Update()
        {
            if (!_open)
            {
                var kb = Keyboard.current;
                // 다른 패널이 떠 있거나 이번 프레임 Esc로 패널을 닫았으면 그 Esc는 이미 쓰였다
                if (kb != null && kb.escapeKey.wasPressedThisFrame && !InputFocus.IsUiOpen && !InputFocus.EscConsumedThisFrame)
                    Open();
                return;
            }
            // 설정이 같은 키로 닫힌 프레임엔 일시정지 창까지 닫지 않는다
            if (_settings.IsOpen || _settings.ClosedThisFrame) return;
            if (!UiCommon.ClosePressed(_openedAt, out bool byEscape)) return;
            if (_confirmWindow.activeSelf) HideConfirm();
            else Close(byEscape);
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            _openedAt = Time.unscaledTime;
            _mainWindow.SetActive(true);
            _confirmWindow.SetActive(false);
            _sessionText.text = DescribeSession();
            _root.SetActive(true);
            InputFocus.PanelOpened();
        }

        public void Close(bool byEscape)
        {
            if (!_open) return;
            if (_settings.IsOpen) _settings.Close();
            _open = false;
            _root.SetActive(false);
            InputFocus.PanelClosed(byEscape);
        }

        private void OpenSettings()
        {
            _mainWindow.SetActive(false);
            _settings.Open();
        }

        private void OnSettingsClosed()
        {
            if (_open) _mainWindow.SetActive(true);
        }

        private void ShowConfirm()
        {
            var nm = NetworkManager.Singleton;
            _confirmText.text = nm != null && nm.IsHost
                ? Loc.T("호스트가 나가면 친구들의 세션도 끝나요.\n메인 메뉴로 나갈까요?")
                : Loc.T("메인 메뉴로 나갈까요?\n친구들은 계속 플레이해요.");
            _mainWindow.SetActive(false);
            _confirmWindow.SetActive(true);
        }

        private void HideConfirm()
        {
            _confirmWindow.SetActive(false);
            _mainWindow.SetActive(true);
        }

        private void LeaveSession()
        {
            Close(false);
            NetworkLauncher.Instance.Shutdown();
        }

        // 클라는 ConnectedClientsIds를 못 읽는다 — 스폰된 플레이어 수로 센다 (열 때 한 번)
        private static string DescribeSession()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return "";
            int players = FindObjectsByType<Player.PlayerController>(FindObjectsSortMode.None).Length;
            return $"{(nm.IsHost ? Loc.T("내가 연 방") : Loc.T("친구 방"))} · {players}/4{Loc.T("명")}";
        }
    }
}
