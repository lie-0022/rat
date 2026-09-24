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
        // 플레이어 없이 이만큼 남은 연결은 끊는다 — 접속 승인 중 끊긴 "유령 연결"이 목록에 남으면
        // 씬 로드 완료 이벤트가 안 와서 스테이지 출발·맵 생성이 멈춘다 (2026-09-24 고양이 67). 느린 로딩은 보통 수 초라 넉넉히
        [SerializeField] private float _ghostSeconds = 20f;

        private readonly System.Collections.Generic.Dictionary<ulong, float> _noPlayerSince = new();
        private float _nextGhostCheck;

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
            if (nm.SceneManager != null)
                nm.SceneManager.OnLoadComplete -= OnClientSceneLoadComplete;
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || Time.unscaledTime < _nextGhostCheck) return;
            _nextGhostCheck = Time.unscaledTime + 1f;
            if (!IsGameplayScene(SceneManager.GetActiveScene().name)) { _noPlayerSince.Clear(); return; }
            foreach (var client in nm.ConnectedClientsList)
            {
                ulong id = client.ClientId;
                if (id == NetworkManager.ServerClientId || client.PlayerObject != null) { _noPlayerSince.Remove(id); continue; }
                if (!_noPlayerSince.TryGetValue(id, out float since)) { _noPlayerSince[id] = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < _ghostSeconds) continue;
                _noPlayerSince.Remove(id);
                Log.Dev($"유령 연결 정리: client {id} — {_ghostSeconds:0}s 동안 플레이어 없음, 끊음");
                nm.DisconnectClient(id, "플레이어 없이 멈춘 연결 정리");
                break; // 목록이 바뀌었으니 다음 검사에서 계속
            }
        }

        private void OnServerStarted()
        {
            // 개별 클라의 씬 로드 "완료" 시점에 스폰 — 로드 전에 스폰하면 그 클라(소유 물리 시뮬)
            // 화면엔 바닥이 아직 없어서 무한 낙하한다 (첫 4인 테스트에서 실측한 버그)
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnClientSceneLoadComplete;

            // 이미 게임플레이 씬에서 호스트가 시작된 경우(DevSceneAutoBoot) — 로드 이벤트가 안 오므로 즉시 스폰
            if (IsGameplayScene(SceneManager.GetActiveScene().name))
                SpawnIfMissing(NetworkManager.Singleton.LocalClientId);
        }

        private void OnClientSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (!IsGameplayScene(sceneName)) return;
            SpawnIfMissing(clientId);
        }

        private void OnClientConnected(ulong clientId)
        {
            // 스폰은 OnLoadComplete에서만. 여기선 로그만 (접속 자체의 관측용)
            if (!NetworkManager.Singleton.IsServer) return;
            Log.Dev($"클라 접속: client {clientId}");
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

        // 번호로 고르면(clientId % 자리 수) 재접속·실패한 접속으로 번호가 밀려 이미 있는 쥐(보통 호스트) 자리와 겹친다 →
        // 둘이 서로 밀쳐 튕겨 올랐다 떨어져 기절 (고양이 133, 4인에서 client 4 = 호스트 자리). 다른 쥐에게서 가장 먼 자리로.
        private static Vector3 GetSpawnPosition(ulong clientId)
        {
            var points = GameObject.FindGameObjectsWithTag("PlayerSpawn");
            if (points.Length == 0) return new Vector3(clientId * 1.5f, 1f, 0f); // 폴백: 원점 옆으로 나란히
            Transform best = null; float bestGap = -1f;
            foreach (var point in points)
            {
                float gap = float.MaxValue;
                foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
                    if (c.PlayerObject != null) gap = Mathf.Min(gap, Vector3.Distance(c.PlayerObject.transform.position, point.transform.position));
                if (gap > bestGap) { bestGap = gap; best = point.transform; }
            }
            return best.position;
        }
    }
}
