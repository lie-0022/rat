using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Run
{
    public enum RunPhase { Loading, ZoneActive, ZoneCleared, Voting, Transition, Extracted, Wiped }

    /// <summary>
    /// 런의 심장 (docs/09, 호스트 권한, 런 씬 수명). 존 생성(ZoneGenerator)은 태스크 2-1 —
    /// 지금 Transition은 정산 리셋 + 존 인덱스 증가만 (샌드박스 = 존 1개 재사용).
    /// MetaWallet·SaveService(2-4) 전까지 코인은 계산·이벤트 발화까지만.
    /// 시간 제한 없음(v1) — 존 장기화 soft pressure(고양이 순찰 확대)는 2-1에서.
    /// </summary>
    public class RunManager : NetworkBehaviour
    {
        public static RunManager Instance { get; private set; }

        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<int> CurrentZoneIndex = new(0);   // 0-base
        public NetworkVariable<int> CurrentQuota = new(0);
        public NetworkVariable<int> DepositedValue = new(0);     // 이번 존 누계
        public NetworkVariable<RunPhase> Phase = new(RunPhase.Loading);
        public NetworkVariable<int> RunSeed = new(0);

        private int _totalValue;                                 // 런 전체 누계
        private readonly Dictionary<ulong, int> _depositCounts = new();
        private float _phaseTimer;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) { enabled = false; return; }
            EventBus.PlayerDowned += OnPlayerDowned;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer) EventBus.PlayerDowned -= OnPlayerDowned;
        }

        // ---- 호스트 API ----

        public void ServerStartRun(int seed)
        {
            if (!IsServer) return;
            RunSeed.Value = seed;
            CurrentZoneIndex.Value = 0;
            _totalValue = 0;
            _depositCounts.Clear();
            GameStateMachine.Instance.TransitionTo(GameState.InRun);
            BeginZone();
        }

        public void ServerDeposit(int value, ulong byClientId)
        {
            if (!IsServer || Phase.Value != RunPhase.ZoneActive) return;
            DepositedValue.Value += value;
            _totalValue += value;
            _depositCounts[byClientId] = _depositCounts.GetValueOrDefault(byClientId) + 1;
            EventBus.RaiseQuotaChanged(DepositedValue.Value, CurrentQuota.Value);
            Log.Dev($"런 정산: +{value} → {DepositedValue.Value}/{CurrentQuota.Value}");

            if (DepositedValue.Value >= CurrentQuota.Value)
            {
                SetPhase(RunPhase.ZoneCleared); // 쥐구멍 개방 연출 지점 (아트 단계)
                EventBus.RaiseZoneEnded(CurrentZoneIndex.Value, true);
                _phaseTimer = Time.time + 10f;  // 10s 후 투표 (docs/09)
            }
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

        public void ServerBeginExtractionVote()
        {
            if (!IsServer || Phase.Value != RunPhase.ZoneCleared) return;
            SetPhase(RunPhase.Voting);
            GetComponent<ExtractionVote>()?.ServerOpenVote();
        }

        /// <summary>ExtractionVote 결과 통보.</summary>
        public void ServerOnVoteResult(bool goDeeper)
        {
            if (!IsServer || Phase.Value != RunPhase.Voting) return;
            if (goDeeper)
            {
                SetPhase(RunPhase.Transition);
                CurrentZoneIndex.Value++;
                BeginZone(); // 존 재생성은 2-1 — 지금은 같은 공간 재사용
            }
            else
            {
                EndRun(extracted: true);
            }
        }

        // ---- 내부 ----

        private void BeginZone()
        {
            DepositedValue.Value = 0;
            int players = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
            CurrentQuota.Value = _balance.GetQuota(CurrentZoneIndex.Value + 1, players);
            SetPhase(RunPhase.ZoneActive);
            EventBus.RaiseZoneStarted(CurrentZoneIndex.Value);
            EventBus.RaiseQuotaChanged(0, CurrentQuota.Value);
            Log.Dev($"존 {CurrentZoneIndex.Value} 시작 — 할당량 {CurrentQuota.Value} ({players}인)");
        }

        private void Update()
        {
            if (Phase.Value == RunPhase.ZoneCleared && Time.time >= _phaseTimer)
                ServerBeginExtractionVote();
        }

        private void OnPlayerDowned(ulong clientId)
        {
            if (Phase.Value is RunPhase.Extracted or RunPhase.Wiped) return;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var condition = client.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerCondition>() : null;
                if (condition != null && condition.State.Value != ConditionState.Downed)
                    return; // 한 명이라도 살아 있으면 계속
            }
            EndRun(extracted: false); // 전멸 (솔로 다운 즉시 여기 걸림 — docs/09)
        }

        private void EndRun(bool extracted)
        {
            int zonesCleared = CurrentZoneIndex.Value + (Phase.Value is RunPhase.ZoneCleared or RunPhase.Voting ? 1 : 0);
            int coins = extracted
                ? _balance.GetExtractCoins(_totalValue, zonesCleared)
                : _balance.GetWipeCoins(_totalValue);
            SetPhase(extracted ? RunPhase.Extracted : RunPhase.Wiped);

            var result = new RunResult
            {
                Extracted = extracted,
                ZonesCleared = zonesCleared,
                TotalValue = _totalValue,
                CoinsAwarded = coins,
                DepositCounts = new Dictionary<ulong, int>(_depositCounts),
                NewCodexIds = new List<string>() // 도감 언락은 2-5
            };
            EventBus.RaiseRunEnded(result);
            EventBus.RaiseCheeseCoinChanged(coins); // MetaWallet 적립은 2-4
            Log.Dev($"런 종료: {(extracted ? "탈출" : "전멸")} — 존 {zonesCleared}개, 가치 {_totalValue}, 코인 {coins}");

            GameStateMachine.Instance.TransitionTo(GameState.RunResult);
            NetworkManager.SceneManager.LoadScene("Hub", UnityEngine.SceneManagement.LoadSceneMode.Single);
            GameStateMachine.Instance.TransitionTo(GameState.Lobby);
        }

        private void SetPhase(RunPhase next)
        {
            if (Phase.Value == next) return;
            Log.Dev($"런 페이즈: {Phase.Value} → {next}");
            Phase.Value = next;
        }
    }
}
