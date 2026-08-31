using RatGame.Net;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Core
{
    /// <summary>
    /// 아무 씬에서나 Play 해도 게임이 돌게 하는 개발 안전망.
    /// Boot 씬을 거치지 않았으면 (NetworkManager 부재) 매니저·NetworkManager를 만들고,
    /// 게임플레이 씬이면 자동으로 호스트까지 시작한다.
    /// "Boot에서 시작 안 하면 아무것도 안 되는" 함정 제거 — 에디터·빌드 공통.
    /// </summary>
    public static class DevSceneAutoBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBooted()
        {
            if (NetworkManager.Singleton != null) return; // 정상 Boot 흐름
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "Boot") return; // GameBootstrap이 처리

            Log.Dev($"[AutoBoot] Boot 씬 없이 시작됨 ({sceneName}) — 매니저 자동 생성");

            if (GameStateMachine.Instance == null)
                new GameObject("GameStateMachine").AddComponent<GameStateMachine>();
            if (NetworkLauncher.Instance == null)
                new GameObject("NetworkLauncher").AddComponent<NetworkLauncher>();

            var prefab = Resources.Load<GameObject>("NetworkManager");
            if (prefab == null) { Log.Error("[AutoBoot] Resources/NetworkManager 프리팹 없음"); return; }
            Object.Instantiate(prefab).name = "NetworkManager";

            GameStateMachine.Instance.TransitionTo(GameState.MainMenu);

            // 게임플레이 씬이면 즉시 호스트 (테스트 흐름 단축)
            if (sceneName != "MainMenu")
            {
                var launcher = NetworkLauncher.Instance;
                launcher.StartCoroutineHost();
            }
        }
    }
}
