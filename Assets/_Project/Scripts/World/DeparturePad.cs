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
        // 목적지 목록 (고양이 59) — 0번이 기본. 기지 목적지 게시판(StageBoard)으로 돌린다
        [SerializeField] private string[] _stageScenes = { "Stage_Warehouse01", "Stage_Generated" };
        [SerializeField] private string[] _stageNames = { "창고", "부엌(생성)" };

        public NetworkVariable<int> ReadyCount = new(0);
        public NetworkVariable<int> NeededCount = new(0);
        public NetworkVariable<bool> Counting = new(false);
        public NetworkVariable<double> DepartAt = new(0);   // 카운트다운 끝 (ServerTime)
        /// <summary>기지 화면 누계 표시용 — 저장값은 호스트에만 있어 클라가 읽도록 복제.</summary>
        public NetworkVariable<int> TotalValue = new(0);
        /// <summary>출발할 스테이지 (_stageScenes 인덱스). 클라도 게시판·RunBar에 이름을 띄운다.</summary>
        public NetworkVariable<int> Destination = new(0);

        public string DestinationName => _stageNames != null && Destination.Value >= 0 && Destination.Value < _stageNames.Length ? _stageNames[Destination.Value] : "?";

        private BoxCollider _box;
        private bool _departing;

        public override void OnNetworkSpawn()
        {
            _box = GetComponent<BoxCollider>();
            if (!IsServer) { enabled = false; return; } // 클라는 NetworkVariable만 읽는다
            TotalValue.Value = RunSession.TotalValue;
            Destination.Value = Mathf.Clamp(RunSession.StageChoice, 0, _stageScenes.Length - 1);
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
            // 기지 씬을 바로 플레이하면 호스트가 뜨기 전 프레임에도 돈다 — 스폰 전엔 NetworkVariable을 쓰지 않는다
            if (_departing || !IsSpawned) return;
            // 상점 구매로 누계가 줄면 바로 비춘다 (같으면 안 보냄)
            if (TotalValue.Value != RunSession.TotalValue) TotalValue.Value = RunSession.TotalValue;
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

        /// <summary>호스트: 목적지를 다음 것으로. 카운트다운 중엔 안 바뀐다 (발판 위 동료가 모르는 곳으로 가지 않게).</summary>
        public bool ServerCycleDestination()
        {
            if (!IsServer || Counting.Value || _departing || _stageScenes.Length < 2) return false;
            Destination.Value = (Destination.Value + 1) % _stageScenes.Length;
            RunSession.StageChoice = Destination.Value;
            Log.Dev($"목적지: {DestinationName} ({_stageScenes[Destination.Value]})");
            return true;
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
            string scene = _stageScenes[Mathf.Clamp(Destination.Value, 0, _stageScenes.Length - 1)];
            Log.Dev($"출발 → {scene} (누계 {RunSession.TotalValue})");
            NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }
    }
}
