using RatGame.Data;
using RatGame.Noise;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 고양이 감각 (docs/07, 호스트 전용). 시야 0.2s 틱 + NoiseSystem 청각 구독 → 의심 게이지.
    /// 게이지는 NetworkVariable — 타깃 HUD "?"/"!" 표시는 UI 태스크(2-6).
    /// LightZone(어둠 50%)은 방 모듈 생기는 2-1에서.
    /// </summary>
    public class CatSenses : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<float> SuspicionGauge = new NetworkVariable<float>(0f);

        /// <summary>Sleep 상태 등에서 CatBrain이 조정 (docs/07 — 수면 민감도 30%).</summary>
        public float SensitivityMultiplier { get; set; } = 1f;

        // 시야 결과 (CatBrain이 읽음)
        public PlayerCondition VisibleTarget { get; private set; }
        public float ContinuousSightSeconds { get; private set; }
        public bool CloseSight { get; private set; }        // 2m 내 목격 — 즉시 추격
        public Vector3 LastStimulusPos { get; private set; }
        public bool HasNewStimulus { get; private set; }    // Suspicious 전이용 (Brain이 소비)
        public bool ImmediateInvestigate { get; private set; } // Break/Trap/Squeak (Brain이 소비)

        private float _nextVisionTick;
        private float _lastStimulusTime;

        public override void OnNetworkSpawn()
        {
            if (IsServer) NoiseSystem.OnNoise += OnNoise;
            else enabled = false;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NoiseSystem.OnNoise -= OnNoise;
        }

        public void ConsumeStimulus() { HasNewStimulus = false; ImmediateInvestigate = false; }

        private void Update()
        {
            if (Time.time >= _nextVisionTick)
            {
                _nextVisionTick = Time.time + 0.2f;
                VisionTick(0.2f);
            }
            // 자극 없을 때 게이지 감쇠 (docs/07)
            if (VisibleTarget == null && Time.time - _lastStimulusTime > 0.5f && SuspicionGauge.Value > 0f)
                SuspicionGauge.Value = Mathf.Max(0f, SuspicionGauge.Value - _balance.CatGaugeDecayPerSec * Time.deltaTime);
        }

        private void VisionTick(float dt)
        {
            PlayerCondition seen = null;
            float bestDist = float.MaxValue;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var playerObj = client.PlayerObject;
                if (playerObj == null) continue;
                var condition = playerObj.GetComponent<PlayerCondition>();
                if (condition == null || condition.State.Value != ConditionState.Active) continue;

                Vector3 toPlayer = playerObj.transform.position - transform.position;
                float dist = toPlayer.magnitude;

                // 웅크림(동기화 스케일)이면 시야 거리 절반 (docs/07)
                bool crouching = playerObj.transform.localScale.y < 0.75f;
                float viewDist = _balance.CatViewDistance * (crouching ? _balance.CatCrouchViewMultiplier : 1f)
                                 * SensitivityMultiplier;
                if (dist > viewDist) continue;
                if (Vector3.Angle(transform.forward, toPlayer) > _balance.CatViewHalfAngle) continue;

                int blockMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker", "Default");
                if (Physics.Linecast(transform.position + Vector3.up * 0.5f,
                        playerObj.transform.position, blockMask, QueryTriggerInteraction.Ignore)) continue;

                if (dist < bestDist) { bestDist = dist; seen = condition; }
            }

            if (seen != null)
            {
                bool sameTarget = seen == VisibleTarget;
                VisibleTarget = seen;
                ContinuousSightSeconds = sameTarget ? ContinuousSightSeconds + dt : dt;
                CloseSight = bestDist <= _balance.CatCloseSightDistance;
                // 거리보정: 가까울수록 큼 (1~2배)
                float distFactor = Mathf.Lerp(2f, 1f, bestDist / _balance.CatViewDistance);
                AddSuspicion(_balance.CatGazeGainPerSec * dt * distFactor, seen.transform.position);
            }
            else
            {
                VisibleTarget = null;
                ContinuousSightSeconds = 0f;
                CloseSight = false;
            }
        }

        private void OnNoise(NoiseEvent e)
        {
            if (!IsServer) return;
            float heard = NoiseSystem.GetLoudnessAt(e, transform.position) * SensitivityMultiplier;
            if (heard < _balance.CatHearThreshold) return;

            // 거리감쇠 (docs/07): 반경 대비 멀수록 약하게
            float radius = NoiseSystem.GetRadius(e);
            float dist = Vector3.Distance(e.Pos, transform.position);
            float distFactor = Mathf.Clamp(1f - dist / Mathf.Max(radius, 0.01f), 0.2f, 1f);
            AddSuspicion(heard * distFactor * _balance.CatHearingGain, e.Pos);

            if (e.Type == NoiseType.Break || e.Type == NoiseType.Trap || e.Type == NoiseType.Squeak)
                ImmediateInvestigate = true; // 즉시 조사 트리거 (docs/06·07)
        }

        private void AddSuspicion(float amount, Vector3 pos)
        {
            SuspicionGauge.Value = Mathf.Min(_balance.CatChaseThreshold, SuspicionGauge.Value + amount);
            LastStimulusPos = pos;
            _lastStimulusTime = Time.time;
            HasNewStimulus = true;
        }
    }
}
