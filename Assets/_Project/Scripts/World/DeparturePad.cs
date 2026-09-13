using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.World
{
    /// <summary>
    /// 기지 출발 발판 (docs/11, 호스트 권한). 다운 안 된 전원이 올라서면 카운트다운 → 스테이지 씬 로드.
    /// 한 명이라도 내려오면 취소. 스테이지 쪽은 RunManager가 전원 로드 완료 후 시작 위치로 옮기고 자동 출발한다.
    /// 기지에 도착했을 때(귀환·전멸 복귀) 전원을 기지 시작 위치로 옮기는 것도 여기서 한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DeparturePad : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private string _stageScene = "Stage_Warehouse01";

        public NetworkVariable<int> ReadyCount = new(0);
        public NetworkVariable<int> NeededCount = new(0);
        public NetworkVariable<bool> Counting = new(false);
        public NetworkVariable<double> DepartAt = new(0);   // 카운트다운 끝 (ServerTime)
        /// <summary>기지 화면 누계 표시용 — 저장값은 호스트에만 있어 클라가 읽도록 복제.</summary>
        public NetworkVariable<int> TotalValue = new(0);

        private BoxCollider _box;
        private bool _departing;

        public override void OnNetworkSpawn()
        {
            _box = GetComponent<BoxCollider>();
            if (!IsServer) { enabled = false; return; } // 클라는 NetworkVariable만 읽는다
            TotalValue.Value = RunSession.TotalValue;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnBaseLoaded;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnBaseLoaded;
        }

        // 기지 도착(첫 입장·귀환·전멸 복귀): 플레이어는 스테이지 좌표에 남아 있으므로 기지 시작 위치로 옮긴다
        private void OnBaseLoaded(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> completed,
                                  System.Collections.Generic.List<ulong> timedOut)
        {
            if (sceneName != gameObject.scene.name) return;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnBaseLoaded;
            PlayerPlacement.TeleportAllToSpawns();
        }

        private void Update()
        {
            if (_departing) return;
            GatherCheck.Count(_box, out int ready, out int needed);
            ReadyCount.Value = ready;
            NeededCount.Value = needed;

            bool allGathered = needed > 0 && ready == needed;
            if (!Counting.Value)
            {
                if (!allGathered) return;
                DepartAt.Value = NetworkManager.ServerTime.Time + _balance.DepartCountdownSeconds;
                Counting.Value = true;
                Log.Dev($"출발 카운트다운 — {needed}명 집합");
            }
            else if (!allGathered)
            {
                Counting.Value = false;
                Log.Dev("출발 취소 — 발판을 벗어남");
            }
            else if (NetworkManager.ServerTime.Time >= DepartAt.Value)
            {
                Depart();
            }
        }

        private void Depart()
        {
            _departing = true;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
                if (carry != null) carry.ServerForceDrop(); // 기지 연습용 물건이 스테이지로 따라가지 않게
            }
            RunSession.DepartPending = true;
            Log.Dev($"출발 → {_stageScene} (누계 {RunSession.TotalValue})");
            NetworkManager.SceneManager.LoadScene(_stageScene, LoadSceneMode.Single);
        }
    }
}
