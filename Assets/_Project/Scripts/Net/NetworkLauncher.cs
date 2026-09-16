using System;
using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using RatGame.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.Net
{
    /// <summary>
    /// 호스트/참가/종료 진입점 (docs/03). 트랜스포트: Steam(Facepunch, 친구 로비) 또는 UnityTransport(127.0.0.1).
    /// 기본값: 빌드=Steam, 에디터=Unity(메뉴 Tools/RatGame/Net/Use Steam In Editor로 켬). 커맨드라인 -unitytransport로 Unity 강제.
    /// Steam 초기화가 실패하면 Unity 로컬 모드로 계속한다.
    /// </summary>
    public class NetworkLauncher : MonoBehaviour
    {
        public static NetworkLauncher Instance { get; private set; }

#if UNITY_EDITOR
        public const string EditorSteamPrefKey = "RatGame.UseSteamInEditor";
#endif

        public event Action<string> LaunchFailed;
        /// <summary>메뉴 버튼이 아닌 곳(Steam 초대 수락)에서 참가가 시작됨 — 인자: 초대한 친구 이름.</summary>
        public event Action<string> JoinStarted;

        private const string LoopbackAddress = "127.0.0.1";
        private const ushort Port = 7777;
        private const int MaxPlayers = 4; // docs/00 — 새 수치 아님(구조 상수)
        // 참가 실패를 빨리 알리려고 — 기본값(60회×1초)이면 메뉴가 1분 가까이 "접속 중"에 묶인다. 10회×0.5초 ≈ 5초
        private const int JoinConnectAttempts = 10;
        private const int JoinConnectTimeoutMs = 500;
        private const string MainMenuSceneName = "MainMenu";

        private SteamLobbyService _steam;
        private ulong _pendingLobbyFromArgs;
        private string _exitReason;

        public bool UsingSteam => _steam != null && _steam.IsAvailable;
        public SteamLobbyService Steam => _steam;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환에 파괴되면 종료 정리가 안 불려 소켓이 샌다

            if (WantsSteam())
            {
                _steam = new SteamLobbyService();
                if (_steam.TryInit())
                {
                    _steam.JoinRequested += OnSteamJoinRequested;
                    _pendingLobbyFromArgs = SteamLobbyService.ParseConnectLobbyArg(Environment.GetCommandLineArgs());
                }
                else
                {
                    _steam.Dispose();
                    _steam = null;
                }
            }
            Log.Dev($"트랜스포트: {(UsingSteam ? $"Steam ({_steam.PersonaName})" : "UnityTransport (로컬)")}");
        }

        private static bool WantsSteam()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-unitytransport") >= 0) return false;
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(EditorSteamPrefKey, false);
#else
            return true;
#endif
        }

        private void Update()
        {
            _steam?.Tick();
            // 게임이 꺼진 채 초대를 수락해 실행된 경우 — 메인 메뉴가 뜬 뒤 참가
            if (_pendingLobbyFromArgs != 0 && SceneManager.GetActiveScene().name == MainMenuSceneName && NetworkManager.Singleton != null)
            {
                ulong lobbyId = _pendingLobbyFromArgs;
                _pendingLobbyFromArgs = 0;
                _ = JoinFromInviteAsync(lobbyId, "친구");
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            CleanupTransport();
            DisposeSteam();
        }

        // 플레이 종료 시 NGO의 지연 셧다운이 완료되지 못하면 UTP 소켓(7777)이 에디터 프로세스에
        // 남아 다음 세션 바인딩이 실패한다 — 트랜스포트를 즉시 닫아 방지.
        private void OnApplicationQuit()
        {
            CleanupTransport();
            DisposeSteam();
        }

        private void DisposeSteam()
        {
            if (_steam == null) return;
            _steam.JoinRequested -= OnSteamJoinRequested;
            _steam.Dispose();
            _steam = null;
        }

        private void CleanupTransport()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsListening) nm.Shutdown(true);
            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null) utp.Shutdown();
        }

        /// <summary>DevSceneAutoBoot용 — NetworkManager 초기화(Awake) 한 프레임 뒤 호스트 시작.</summary>
        public void StartCoroutineHost() => StartCoroutine(DelayedHost());

        private System.Collections.IEnumerator DelayedHost()
        {
            yield return null;
            var task = StartHostAsync();
            yield return new WaitUntil(() => task.IsCompleted);
            Log.Dev($"[AutoBoot] 자동 호스트 {(task.Result ? "성공" : "실패")}");
        }

        public async Task<bool> StartHostAsync()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening) return false;
            if (!ConfigureTransport(nm)) return Fail("네트워크 설정을 찾지 못했어요.");

            // Steam: 친구가 초대받아 들어올 로비를 먼저 만든다
            if (UsingSteam && !await _steam.CreateLobbyAsync(MaxPlayers))
                return Fail("Steam 방을 만들지 못했어요. Steam 연결을 확인하세요.");

            nm.ConnectionApprovalCallback = ApproveConnection;
            if (!nm.StartHost())
            {
                _steam?.LeaveLobby();
                return Fail("호스트 시작 실패");
            }
            Log.Dev($"호스트 시작 ({TransportName})");
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
            return true;
        }

        /// <summary>참가. Steam 모드는 lobbyId(초대받은 로비) 필수, 로컬 모드는 무시하고 127.0.0.1.</summary>
        public async Task<bool> JoinAsync(ulong lobbyId = 0)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening) return false;
            if (!ConfigureTransport(nm)) return Fail("네트워크 설정을 찾지 못했어요.");

            if (UsingSteam)
            {
                if (lobbyId == 0) return Fail("친구가 보낸 Steam 초대를 수락하면 들어갈 수 있어요.");
                ulong owner = await _steam.JoinLobbyAsync(lobbyId);
                if (owner == 0) return Fail("친구 방에 들어가지 못했어요. 방이 닫혔거나 가득 찼을 수 있어요.");
                if (owner == _steam.MySteamId)
                {
                    _steam.LeaveLobby();
                    return Fail("내가 연 방에는 참가할 수 없어요.");
                }
                nm.GetComponent<FacepunchTransport>().targetSteamId = owner;
            }

            if (!nm.StartClient())
            {
                _steam?.LeaveLobby();
                return Fail("클라이언트 접속 실패");
            }
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            Log.Dev($"클라이언트 접속 시도 ({TransportName})");
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
            return true;
        }

        private void OnSteamJoinRequested(ulong lobbyId, string friendName) => _ = JoinFromInviteAsync(lobbyId, friendName);

        private async Task JoinFromInviteAsync(ulong lobbyId, string friendName)
        {
            var nm = NetworkManager.Singleton;
            // 세션 중 수락은 받지 않는다 — 지금 방을 나간 뒤 다시 수락 (호스트 마이그레이션·중간 이동 없음, docs/00)
            if (nm == null || nm.IsListening || SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                Log.Dev($"세션 중 Steam 초대 수락 — 무시 ({friendName})");
                return;
            }
            Log.Dev($"Steam 초대 수락: {friendName}의 방 ({lobbyId})");
            JoinStarted?.Invoke(friendName);
            await JoinAsync(lobbyId);
        }

        /// <summary>메인 메뉴가 한 번 보여줄 세션 종료 사유 (호스트가 나감 등). 읽으면 비운다.</summary>
        public string ConsumeExitReason()
        {
            string reason = _exitReason;
            _exitReason = null;
            return reason;
        }

        // 세션 중(메뉴 밖) 호스트가 나가거나 연결이 끊기면 멈춘 기지에 남지 않고 메인 메뉴로 — 메뉴의 참가 실패는 MainMenuController가 처리
        private void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || clientId != nm.LocalClientId) return;
            if (SceneManager.GetActiveScene().name == MainMenuSceneName)
            {
                _steam?.LeaveLobby(); // 참가 실패 — 들어갔던 로비에서도 나온다
                return;
            }
            _exitReason = "호스트와 연결이 끊겨 메인 메뉴로 돌아왔어요.";
            Shutdown();
        }

        /// <summary>세션 정리 후 메인 메뉴로 (일시정지 창 "세션 나가기", 호스트 이탈).</summary>
        public void Shutdown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= OnClientDisconnected; // 스스로 나갈 때 끊김 처리로 다시 들어오지 않게
            if (nm != null && nm.IsListening) nm.Shutdown();
            _steam?.LeaveLobby();
            Log.Dev("네트워크 종료 — MainMenu 복귀");
            if (GameStateMachine.Instance.Current != GameState.MainMenu)
                GameStateMachine.Instance.TransitionTo(GameState.MainMenu);
            SceneManager.LoadScene(MainMenuSceneName);
        }

        private string TransportName => UsingSteam ? "Steam" : "UnityTransport";

        private bool ConfigureTransport(NetworkManager nm)
        {
            if (UsingSteam)
            {
                var facepunch = nm.GetComponent<FacepunchTransport>();
                if (facepunch == null)
                {
                    Log.Error("NetworkManager에 FacepunchTransport가 없음 — 프리팹 확인");
                    return false;
                }
                nm.NetworkConfig.NetworkTransport = facepunch;
                return true;
            }

            var utp = nm.GetComponent<UnityTransport>();
            if (utp == null) return false;
            utp.SetConnectionData(LoopbackAddress, Port);
            utp.MaxConnectAttempts = JoinConnectAttempts;
            utp.ConnectTimeoutMS = JoinConnectTimeoutMs;
            nm.NetworkConfig.NetworkTransport = utp;
            return true;
        }

        private bool Fail(string message)
        {
            Log.Dev($"네트워크 시작 실패: {message}");
            LaunchFailed?.Invoke(message);
            return false;
        }

        // ConnectionApproval (docs/03): 최대 4명, 게임 진행 중(midgame) 참가 거부. 스폰은 NetPlayerSpawner 수동.
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
                                       NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;

            if (NetworkManager.Singleton.ConnectedClientsIds.Count >= MaxPlayers)
            {
                response.Approved = false;
                response.Reason = "정원 초과 (최대 4명)";
                return;
            }
            if (GameStateMachine.Instance.Current == GameState.InRun)
            {
                response.Approved = false;
                response.Reason = "게임 진행 중에는 참가할 수 없음 (로비에서만 합류)";
                return;
            }
            response.Approved = true;
        }
    }
}
