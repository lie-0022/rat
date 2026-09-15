using System;
using System.Threading.Tasks;
using RatGame.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 호스트/참가/종료 진입점 (docs/03). W1은 UnityTransport(127.0.0.1) 전용 —
    /// Facepunch(Steam Relay) 분기는 태스크 0-6에서 이 클래스에 추가한다.
    /// 트랜스포트 기본값: 에디터=Unity, 빌드=Steam. 커맨드라인 -unitytransport로 강제 (docs/03).
    /// </summary>
    public class NetworkLauncher : MonoBehaviour
    {
        public static NetworkLauncher Instance { get; private set; }

        public event Action<string> LaunchFailed;

        private const string LoopbackAddress = "127.0.0.1";
        private const ushort Port = 7777;
        private const int MaxPlayers = 4; // docs/00 — 새 수치 아님(구조 상수)
        // 참가 실패를 빨리 알리려고 — 기본값(60회×1초)이면 메뉴가 1분 가까이 "접속 중"에 묶인다. 10회×0.5초 ≈ 5초
        private const int JoinConnectAttempts = 10;
        private const int JoinConnectTimeoutMs = 500;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환에 파괴되면 종료 정리가 안 불려 소켓이 샌다
        }

        private void OnDestroy()
        {
            if (Instance == this) CleanupTransport();
        }

        // 플레이 종료 시 NGO의 지연 셧다운이 완료되지 못하면 UTP 소켓(7777)이 에디터 프로세스에
        // 남아 다음 세션 바인딩이 실패한다 — 트랜스포트를 즉시 닫아 방지.
        private void OnApplicationQuit() => CleanupTransport();

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

        public Task<bool> StartHostAsync()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening) return Task.FromResult(false);

            ConfigureTransport(nm);
            nm.ConnectionApprovalCallback = ApproveConnection;

            bool ok = nm.StartHost();
            if (!ok)
            {
                LaunchFailed?.Invoke("호스트 시작 실패");
                return Task.FromResult(false);
            }
            Log.Dev("호스트 시작 (UnityTransport)");
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
            return Task.FromResult(true);
        }

        /// <summary>W1: lobbyId 무시하고 로컬 호스트로 접속. 0-6에서 Steam 로비 참가로 확장.</summary>
        public Task<bool> JoinAsync(ulong lobbyId = 0)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening) return Task.FromResult(false);

            ConfigureTransport(nm);
            bool ok = nm.StartClient();
            if (!ok)
            {
                LaunchFailed?.Invoke("클라이언트 접속 실패");
                return Task.FromResult(false);
            }
            Log.Dev("클라이언트 접속 시도 (UnityTransport)");
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
            return Task.FromResult(true);
        }

        /// <summary>정리 후 복귀. Hub 씬이 생기는 태스크 1-8 전까지는 MainMenu로 돌아간다.</summary>
        public void Shutdown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            Log.Dev("네트워크 종료 — MainMenu 복귀");
            if (GameStateMachine.Instance.Current != GameState.MainMenu)
                GameStateMachine.Instance.TransitionTo(GameState.MainMenu);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private static void ConfigureTransport(NetworkManager nm)
        {
            var utp = nm.GetComponent<UnityTransport>();
            utp.SetConnectionData(LoopbackAddress, Port);
            utp.MaxConnectAttempts = JoinConnectAttempts;
            utp.ConnectTimeoutMS = JoinConnectTimeoutMs;
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
