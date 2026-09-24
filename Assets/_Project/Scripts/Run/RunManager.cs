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
    /// 목표 금액 없음 — 쥐구멍 적립 + 귀환자가 들고 온 물건이 수확. 누계는 저장되고 초기화되지 않는다.
    /// 전원 다운 = 전멸 = 이번 적립만 잃고 기지로.
    /// 스테이지 생성(ZoneGenerator)은 태스크 2-1 — 지금은 맵 1개(Stage_Warehouse01)를 스테이지마다 다시 로드해 쓴다.
    /// </summary>
    public class RunManager : NetworkBehaviour
    {
        public static RunManager Instance { get; private set; }

        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private string _baseScene = "Hub";   // 결과 화면 뒤 돌아갈 기지 씬

        public NetworkVariable<RunPhase> Phase = new(RunPhase.Ready);
        public NetworkVariable<int> RunSeed = new(0);
        public NetworkVariable<int> RunTotalValue = new(0);      // 저장된 누계 (RunSession → SaveService)
        public NetworkVariable<int> StashedValue = new(0);       // 이번 스테이지 쥐구멍 적립
        public NetworkVariable<int> ReturnReadyCount = new(0);   // 쥐구멍 위 인원
        public NetworkVariable<int> ReturnNeededCount = new(0);  // 귀환에 필요한 인원 (다운 제외)
        public NetworkVariable<double> ReturnAt = new(0);        // 귀환 카운트다운 끝 (ServerTime)

        // 결과 화면용 — EventBus는 호스트 로컬이라 클라 HUD가 못 받는다
        public NetworkVariable<int> ResultCarriedValue = new(0);
        public NetworkVariable<double> ResultEndsAt = new(0);
        /// <summary>플레이어별 기여 (결과 화면 "오늘의 쥐들"). 출발 시 접속 인원으로 채우고 적립·귀환·전멸에 갱신.</summary>
        public NetworkList<PlayerContribution> Contributions;

        private DepositZone _returnZone;
        private bool _leaving;
        private bool _returnArmed;

        /// <summary>쥐구멍 적립을 받는 페이즈. 귀환 카운트다운 중에도 받는다.</summary>
        public bool AcceptsDeposits => Phase.Value is RunPhase.StageActive or RunPhase.Returning;
        public bool IsShowingResult => Phase.Value is RunPhase.Returned or RunPhase.Wiped;

        // NetworkList는 스폰 전(Awake)에 만들어야 동기화 대상에 들어간다
        private void Awake() => Contributions = new NetworkList<PlayerContribution>();

        private StageQuota _quota; // 있으면 새 루프 할당량 규칙 (벽 속 스테이지)

        public override void OnNetworkSpawn()
        {
            _quota = GetComponent<StageQuota>();
            Instance = this;
            if (!IsServer) { enabled = false; return; }
            RunTotalValue.Value = RunSession.TotalValue;
            _returnZone = FindFirstObjectByType<DepositZone>();
            if (_returnZone == null) Log.Dev("RunManager: 쥐구멍(DepositZone)이 없어 귀환할 수 없음");
            EventBus.PlayerDowned += OnPlayerDowned;
            Noise.NoiseSystem.Rippled += OnNoiseRipple; // 큰 소음 → 모든 클라 파문 (docs/06)
            // 경계도 디렉터 — 호스트 전용 계산기라 네트워크 컴포넌트가 아니다 (design/cat-ideas/12)
            var director = GetComponent<RunDirector>();
            if (director == null) director = gameObject.AddComponent<RunDirector>();
            director.Init(_balance, this);
            var house = GetComponent<HouseEventDirector>();
            if (house == null) house = gameObject.AddComponent<HouseEventDirector>();
            house.Init(_balance, this);
            var relation = GetComponent<AI.CatRelation>();
            if (relation == null) relation = gameObject.AddComponent<AI.CatRelation>();
            relation.Init(_balance);
            // 기지 발판으로 들어온 경우: 전원이 씬 로드를 끝내면 시작 위치로 옮기고 자동 출발.
            // 에디터에서 스테이지 씬을 직접 플레이하면 로드 이벤트가 없어 Ready로 대기 (개발용 Enter)
            if (RunSession.DepartPending)
                NetworkManager.SceneManager.OnLoadEventCompleted += OnStageLoaded;
        }

        /// <summary>호스트: 집주인 이벤트 예고·시작·끝을 모든 클라에 (토스트). 목소리는 소음이 아니다 — 고양이가 반응하면 안 되는 소리.</summary>
        public void ServerHouseEvent(HouseEventKind kind, HouseEventPhase phase) => HouseEventClientRpc((byte)kind, (byte)phase);

        private void OnNoiseRipple(Vector3 pos, float loudness) { if (IsSpawned) NoiseRippleClientRpc(pos, loudness); }

        [ClientRpc]
        private void NoiseRippleClientRpc(Vector3 pos, float loudness) => EventBus.RaiseNoiseRipple(pos, loudness);

        [ClientRpc]
        private void HouseEventClientRpc(byte kind, byte phase) => EventBus.RaiseHouseEvent((HouseEventKind)kind, (HouseEventPhase)phase);

        public override void OnNetworkDespawn()
        {
            if (IsServer) Noise.NoiseSystem.Rippled -= OnNoiseRipple;
            if (Instance == this) Instance = null;
            if (!IsServer) return;
            EventBus.PlayerDowned -= OnPlayerDowned;
            if (NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnStageLoaded;
        }

        private void OnStageLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != gameObject.scene.name) return;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnStageLoaded;
            RunSession.DepartPending = false;
            PlayerPlacement.TeleportAllToSpawns();
            ServerStartStage(Random.Range(0, int.MaxValue));
        }

        // ---- 호스트 API ----

        /// <summary>파밍 출발 — 기지 발판으로 들어오면 자동 호출. 스테이지 씬 직접 플레이 시엔 개발용 Enter.</summary>
        public void ServerStartStage(int seed)
        {
            if (!IsServer || Phase.Value != RunPhase.Ready) return;
            RunSeed.Value = seed;
            StashedValue.Value = 0;
            Contributions.Clear();
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                Contributions.Add(new PlayerContribution { ClientId = clientId });
            _returnArmed = false;
            GameStateMachine.Instance.TransitionTo(GameState.InRun);
            SetPhase(RunPhase.StageActive);
            EventBus.RaiseZoneStarted(0);
            Log.Dev($"파밍 출발 — 누계 {RunTotalValue.Value}");
        }

        public void ServerDeposit(int value, ulong byClientId)
        {
            if (!IsServer || !AcceptsDeposits) return;
            StashedValue.Value += value;
            int i = ContributionIndex(byClientId);
            var c = Contributions[i];
            c.DepositedValue += value;
            c.DepositCount++;
            Contributions[i] = c;
            Log.Dev($"쥐구멍 적립: +{value} → {StashedValue.Value} (client {byClientId})");
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
            if (_returnZone == null) _returnZone = FindFirstObjectByType<DepositZone>(); // 생성 스테이지는 쥐구멍이 런매니저보다 늦게 생긴다 (고양이 58)
            GatherCheck.Count(_returnZone != null ? _returnZone.Area : null, out int ready, out int needed);
            ReturnNeededCount.Value = needed;
            ReturnReadyCount.Value = ready;

            bool allGathered = needed > 0 && ready == needed;
            if (Phase.Value == RunPhase.StageActive)
            {
                // 씬을 다시 불러와도 플레이어는 제자리 — 쥐구멍 위에서 출발하면 곧장 귀환되므로, 누군가 한 번 벗어나야 귀환 판정 시작
                if (!allGathered) { _returnArmed = true; return; }
                if (!_returnArmed || (_quota != null && !_quota.Met(StashedValue.Value))) return; // 새 루프: 할당량 전엔 못 떠남
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
            // 귀환자가 손·주머니에 든 물건 (대형을 여럿이 들고 있으면 먼저 센 한 명에게 한 번만)
            var counted = new HashSet<CarryableItem>();
            int carriedValue = 0;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                int i = ContributionIndex(client.ClientId);
                var c = Contributions[i];
                var condition = client.PlayerObject.GetComponent<PlayerCondition>();
                c.Downed = condition != null && condition.State.Value == ConditionState.Downed;
                var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
                if (!c.Downed && carry != null)
                {
                    c.CarriedValue += CountItem(carry.CarriedItem, counted);
                    for (int s = 0; s < carry.SlotCount; s++) c.CarriedValue += CountItem(carry.GetSlotItem(s), counted);
                }
                carriedValue += c.CarriedValue;
                Contributions[i] = c;
            }

            int haul = StashedValue.Value + carriedValue;
            if (_quota != null) _quota.ServerOnCleared(haul); else RunSession.AddHaul(haul); // 누계에 더하고 바로 저장
            ResultCarriedValue.Value = carriedValue;
            RunTotalValue.Value = RunSession.TotalValue;
            ResultEndsAt.Value = NetworkManager.ServerTime.Time + _balance.ResultScreenSeconds;
            SetPhase(RunPhase.Returned);
            EventBus.RaiseZoneEnded(0, true);
            Log.Dev($"귀환: 쥐구멍 {StashedValue.Value} + 들고 온 {carriedValue} = {haul}, 누계 {RunTotalValue.Value}");
        }

        private static int CountItem(CarryableItem item, HashSet<CarryableItem> counted) =>
            item != null && counted.Add(item) ? item.EffectiveValue : 0;

        // 출발 뒤 들어온 기여자(개발용 직접 플레이 등)도 줄을 만든다
        private int ContributionIndex(ulong clientId)
        {
            for (int i = 0; i < Contributions.Count; i++)
                if (Contributions[i].ClientId == clientId) return i;
            Contributions.Add(new PlayerContribution { ClientId = clientId });
            return Contributions.Count - 1;
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
            if (_quota != null) _quota.ServerOnWiped();
            ResultEndsAt.Value = NetworkManager.ServerTime.Time + _balance.ResultScreenSeconds;
            var depositCounts = new Dictionary<ulong, int>();
            for (int i = 0; i < Contributions.Count; i++)
            {
                var c = Contributions[i];
                c.Downed = true;
                Contributions[i] = c;
                depositCounts[c.ClientId] = c.DepositCount;
            }
            var result = new RunResult
            {
                TotalValue = RunTotalValue.Value,
                CoinsAwarded = 0, // 메타 보상 규칙 미정 (docs/09)
                DepositCounts = depositCounts,
                NewCodexIds = new List<string>()
            };
            EventBus.RaiseRunEnded(result);
            Log.Dev($"전멸 — 이번 적립 {StashedValue.Value} 잃음, 누계 {RunTotalValue.Value} 유지");
            GameStateMachine.Instance.TransitionTo(GameState.RunResult);
        }

        // 결과 화면 뒤 기지로. 플레이어 오브젝트는 씬을 넘어 유지되므로 손·주머니와 상태이상을 여기서 비운다
        // (전멸 후 다운 상태가 다음 런까지 남던 문제). 기지 도착 위치는 DeparturePad가 잡는다
        private void LeaveStage()
        {
            _leaving = true;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
                if (carry != null) carry.ServerForceDrop(); // 손·주머니 참조가 다음 씬으로 새지 않게
                var condition = client.PlayerObject.GetComponent<PlayerCondition>();
                if (condition != null) condition.ServerSetState(ConditionState.Active);
            }
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
            RunSession.DepartPending = false;
            Log.Dev($"결과 화면 종료 — 기지({_baseScene})로 (누계 {RunSession.TotalValue})");
            NetworkManager.SceneManager.LoadScene(_baseScene, LoadSceneMode.Single);
        }

        private void SetPhase(RunPhase next)
        {
            if (Phase.Value == next) return;
            Log.Dev($"런 페이즈: {Phase.Value} → {next}");
            Phase.Value = next;
        }
    }
}
