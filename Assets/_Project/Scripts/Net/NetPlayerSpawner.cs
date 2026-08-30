using RatGame.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.Net
{
    /// <summary>
    /// 플레이어 수동 스폰 (docs/03 — NetworkManager 자동 스폰 끔). 호스트에서만 동작.
    /// 스폰 시점 2개: ① 씬 로드 완료(호스트 포함 전원 — 씬 전환으로 despawn된 뒤 재스폰),
    /// ② 클라 접속(이미 게임플레이 씬에 있을 때 늦게 합류한 클라).
    /// 메뉴 씬에서는 스폰하지 않는다 — 씬 전환 시 어차피 파괴돼서.
    /// NetworkManager 프리팹에 붙는다 (프리팹 참조가 필요해서 코드 생성 매니저가 아님).
    /// </summary>
    public class NetPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            nm.OnServerStarted += OnServerStarted;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted -= OnServerStarted;
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnServerStarted()
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        }

        private void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
            System.Collections.Generic.List<ulong> clientsCompleted,
            System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            if (!IsGameplayScene(sceneName)) return;
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                SpawnIfMissing(clientId);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (!IsGameplayScene(SceneManager.GetActiveScene().name)) return;
            SpawnIfMissing(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            // 들고 있던 아이템 놓기는 CarryController 생기는 태스크 0-5에서 처리 (docs/03 접속 해제)
            Log.Dev($"클라 이탈: client {clientId}");
        }

        private void SpawnIfMissing(ulong clientId)
        {
            var client = NetworkManager.Singleton.ConnectedClients[clientId];
            if (client.PlayerObject != null) return;

            var spawned = Instantiate(_playerPrefab, GetSpawnPosition(clientId), Quaternion.identity);
            spawned.SpawnAsPlayerObject(clientId);
            Log.Dev($"플레이어 스폰: client {clientId}");
        }

        private static bool IsGameplayScene(string sceneName) =>
            sceneName != "Boot" && sceneName != "MainMenu";

        private static Vector3 GetSpawnPosition(ulong clientId)
        {
            var points = GameObject.FindGameObjectsWithTag("PlayerSpawn");
            if (points.Length > 0)
                return points[(int)(clientId % (ulong)points.Length)].transform.position;
            return new Vector3(clientId * 1.5f, 1f, 0f); // 폴백: 원점 옆으로 나란히
        }
    }
}
