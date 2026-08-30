using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.Core
{
    /// <summary>
    /// Boot 씬(빌드 0번)의 유일한 진입점 (docs/01·02). DontDestroyOnLoad 매니저를 코드에서 생성하고
    /// MainMenu를 로드한다. 씬에는 이 컴포넌트를 가진 오브젝트 하나만 둔다.
    /// SteamClient.Init(480)은 태스크 0-6(Facepunch)에서 여기 추가 — 실패해도 UnityTransport 모드로 계속 (docs/03).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateManagers();
        }

        private void Start()
        {
            Log.Dev("부트스트랩 완료 — MainMenu 로드");
            GameStateMachine.Instance.TransitionTo(GameState.MainMenu);
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        // 매니저는 씬이 아니라 코드에서 생성한다 (docs/02 원칙 4 — 씬에는 콘텐츠만)
        private void CreateManagers()
        {
            CreateManager<GameStateMachine>("GameStateMachine");
            CreateManager<Net.NetworkLauncher>("NetworkLauncher");
            // 이후 태스크에서 추가: SaveService(2-4) 등 — docs/02 싱글톤 허용 목록만
        }

        private static T CreateManager<T>(string objectName) where T : Component
        {
            var go = new GameObject(objectName);
            return go.AddComponent<T>();
        }
    }
}
