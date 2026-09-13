using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.Run
{
    public enum RunPhase { Ready, StageActive, Returning, Returned, Wiped }

    /// <summary>
    /// 런의 심장 (docs/09, 호스트 권한, 런 씬 수명). 레포식 루프: 기지 출발 → 스테이지 파밍 → 전원 쥐구멍 집합 → 귀환.
    /// 목표 금액 없음 — 쥐구멍 적립 + 귀환자가 들고 온 물건이 수확. 전원 다운 = 전멸 = 런 종료.
    /// 스테이지 생성(ZoneGenerator)은 태스크 2-1 — 지금은 같은 샌드박스를 다시 로드해 다음 스테이지로 쓴다.
    /// </summary>
    public class RunManager : NetworkBehaviour
    {
        public static RunManager Instance { get; private set; }

        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<RunPhase> Phase = new(RunPhase.Ready);
        public NetworkVariable<int> RunSeed = new(0);
        public NetworkVariable<int> StageNumber = new(1);        // 1부터
        public NetworkVariable<int> RunTotalValue = new(0);      // 기지로 가져온 런 누계
        public NetworkVariable<int> StashedValue = new(0);       // 이번 스테이지 쥐구멍 적립
        public NetworkVariable<int> ReturnReadyCount = new(0);   // 쥐구멍 위 인원
        public NetworkVariable<int> ReturnNeededCount = new(0);  // 귀환에 필요한 인원 (다운 제외)
        public NetworkVariable<double> ReturnAt = new(0);        // 귀환 카운트다운 끝 (ServerTime)

        // 결과 화면용 — EventBus는 호스트 로컬이라 클라 HUD가 못 받는다
        public NetworkVariable<int> ResultCarriedValue = new(0);
        public NetworkVariable<double> ResultEndsAt = new(0);

        private readonly Dictionary<ulong, int> _depositCounts = new();
        private DepositZone _returnZone;
        private bool _leaving;
        private bool _returnArmed;

        /// <summary>쥐구멍 적립을 받는 페이즈. 귀환 카운트다운 중에도 받는다.</summary>
        public bool AcceptsDeposits => Phase.Value is RunPhase.StageActive or RunPhase.Returning;
        public bool IsShowingResult => Phase.Value is RunPhase.Returned or RunPhase.Wiped;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) { enabled = false; return; }
            StageNumber.Value = RunSession.StageNumber;
            RunTotalValue.Value = RunSession.TotalValue;
            _returnZone = FindFirstObjectByType<DepositZone>();
            if (_returnZone == null) Log.Dev("RunManager: 쥐구멍(DepositZone)이 없어 귀환할 수 없음");
            EventBus.PlayerDowned += OnPlayerDowned;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer) EventBus.PlayerDowned -= OnPlayerDowned;
        }

        // ---- 호스트 API ----

        /// <summary>기지에서 출발 (지금은 샌드박스 Ready에서 호스트 Enter).</summary>
        public void ServerStartStage(int seed)
        {
            if (!IsServer || Phase.Value != RunPhase.Ready) return;
            RunSeed.Value = seed;
            StashedValue.Value = 0;
            _depositCounts.Clear();
            _returnArmed = false;
            GameStateMachine.Instance.TransitionTo(GameState.InRun);
            SetPhase(RunPhase.StageActive);
            EventBus.RaiseZoneStarted(StageNumber.Value - 1);
            Log.Dev($"스테이지 {StageNumber.Value} 출발 — 누계 {RunTotalValue.Value}");
        }

        public void ServerDeposit(int value, ulong byClientId)
        {
            if (!IsServer || !AcceptsDeposits) return;
            StashedValue.Value += value;
            _depositCounts[byClientId] = _depositCounts.GetValueOrDefault(byClientId) + 1;
            Log.Dev($"쥐구멍 적립: +{value} → {StashedValue.Value}");
        }

        /// <summary>다운 몸이 쥐구멍 진입 시 (DepositZone 분기 — docs/08·09).</summary>
        public void ServerRevive(ulong clientId)
        {
            if (!IsServer) return;
            var client = NetworkManager.ConnectedClients.GetValueOrDefault(clientId);
            var condition = client?.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerCondition>() : null;
            if (condition == null || condition.State.Value != ConditionState.Downed) return;
            condition.ServerSetState(ConditionState.Active); // 스태미나 50 복귀는 PlayerStamina 확장 시
            Log.Dev($"부활: client {clientId}");
        }

        // ---- 내부 ----

        private void Update()
        {
            switch (Phase.Value)
            {
                case RunPhase.StageActive:
                case RunPhase.Returning:
                    UpdateReturn();
                    break;
                case RunPhase.Returned:
                case RunPhase.Wiped:
                    if (!_leaving && NetworkManager.ServerTime.Time >= ResultEndsAt.Value) LeaveStage();
                    break;
            }
        }

        // 다운 안 된 전원이 쥐구멍 위면 카운트다운, 한 명이라도 벗어나면 취소
        private void UpdateReturn()
        {
            int needed = 0, ready = 0;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var condition = client.PlayerObject.GetComponent<PlayerCondition>();
                if (condition != null && condition.State.Value == ConditionState.Downed) continue; // 쓰러진 사람은 두고 간다
                needed++;
                if (_returnZone != null && _returnZone.ContainsFootprint(client.PlayerObject.transform.position)) ready++;
            }
            ReturnNeededCount.Value = needed;
            ReturnReadyCount.Value = ready;

            bool allGathered = needed > 0 && ready == needed;
            if (Phase.Value == RunPhase.StageActive)
            {
                // 씬을 다시 불러와도 플레이어는 제자리 — 쥐구멍 위에서 출발하면 곧장 귀환되므로, 누군가 한 번 벗어나야 귀환 판정 시작
                if (!allGathered) { _returnArmed = true; return; }
                if (!_returnArmed) return;
                ReturnAt.Value = NetworkManager.ServerTime.Time + _balance.ReturnCountdownSeconds;
                SetPhase(RunPhase.Returning);
            }
            else if (!allGathered)
            {
                SetPhase(RunPhase.StageActive);
                Log.Dev("귀환 취소 — 쥐구멍을 벗어남");
            }
            else if (NetworkManager.ServerTime.Time >= ReturnAt.Value)
            {
                CompleteReturn();
            }
        }

        private void CompleteReturn()
        {
            // 귀환자가 손·주머니에 든 물건 (대형을 여럿이 들고 있으면 한 번만)
            var carried = new HashSet<CarryableItem>();
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var condition = client.PlayerObject.GetComponent<PlayerCondition>();
                if (condition != null && condition.State.Value == ConditionState.Downed) continue;
                var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
                if (carry == null) continue;
                if (carry.CarriedItem != null) carried.Add(carry.CarriedItem);
                for (int i = 0; i < PlayerCarryController.SlotCount; i++)
                {
                    var item = carry.GetSlotItem(i);
                    if (item != null) carried.Add(item);
                }
            }
            int carriedValue = 0;
            foreach (var item in carried) carriedValue += item.EffectiveValue;

            int haul = StashedValue.Value + carriedValue;
            RunSession.AdvanceStage(haul);
            ResultCarriedValue.Value = carriedValue;
            RunTotalValue.Value = RunSession.TotalValue;
            ResultEndsAt.Value = NetworkManager.ServerTime.Time + _balance.ResultScreenSeconds;
            SetPhase(RunPhase.Returned);
            EventBus.RaiseZoneEnded(StageNumber.Value - 1, true);
            Log.Dev($"귀환: 스테이지 {StageNumber.Value} — 쥐구멍 {StashedValue.Value} + 들고 온 {carriedValue} = {haul}, 누계 {RunTotalValue.Value}");
        }

        private void OnPlayerDowned(ulong clientId)
        {
            if (!AcceptsDeposits) return; // 스테이지 중에만 전멸 판정
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var condition = client.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerCondition>() : null;
                if (condition != null && condition.State.Value != ConditionState.Downed)
                    return; // 한 명이라도 살아 있으면 계속
            }
            Wipe(); // 솔로 다운 즉시 여기 걸림 — docs/09
        }

        private void Wipe()
        {
            SetPhase(RunPhase.Wiped);
            ResultEndsAt.Value = NetworkManager.ServerTime.Time + _balance.ResultScreenSeconds;
            var result = new RunResult
            {
                ZonesCleared = StageNumber.Value - 1,
                TotalValue = RunTotalValue.Value,
                CoinsAwarded = 0, // 메타 보상 규칙 미정 (docs/09)
                DepositCounts = new Dictionary<ulong, int>(_depositCounts),
                NewCodexIds = new List<string>()
            };
            EventBus.RaiseRunEnded(result);
            RunSession.Reset();
            Log.Dev($"전멸 — 스테이지 {StageNumber.Value}에서 런 종료, 가져온 누계 {RunTotalValue.Value}");
            GameStateMachine.Instance.TransitionTo(GameState.RunResult);
        }

        // 결과 화면 뒤: 기지 씬이 아직 없어 런 씬(현재 씬)을 다시 연다 → 물건·RunManager 초기화, RunSession이 다음 스테이지를 넘겨줌.
        // 기지 씬 구현 시 이 지점을 기지 씬 로드로 바꾼다
        private void LeaveStage()
        {
            _leaving = true;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
                if (carry != null) carry.ServerForceDrop(); // 손·주머니 참조가 다음 씬으로 새지 않게
            }
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
            string scene = SceneManager.GetActiveScene().name;
            Log.Dev($"결과 화면 종료 — {scene} 다시 로드 (다음: 스테이지 {RunSession.StageNumber})");
            NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        private void SetPhase(RunPhase next)
        {
            if (Phase.Value == next) return;
            Log.Dev($"런 페이즈: {Phase.Value} → {next}");
            Phase.Value = next;
        }
    }
}
