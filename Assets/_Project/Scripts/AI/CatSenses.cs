using RatGame.Data;
using RatGame.Noise;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 고양이 감각 (docs/07, 호스트 전용). 시야 0.2s 틱 + NoiseSystem 청각 구독 → 의심 게이지.
    /// 게이지는 NetworkVariable — 타깃 HUD "?"/"!" 표시는 UI 태스크(2-6).
    /// 어둠: 쥐가 꺼진 LightZone 안이면 시야 거리 × catDarkViewMultiplier (2026-09-24, 고양이 31).
    /// 세 번째 감각 — 움직임(2026-09-24, design/cat-ideas/03): 시야 안에서 굴러가는 풀린 물건 → CuriosityTarget.
    /// </summary>
    public class CatSenses : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<float> SuspicionGauge = new NetworkVariable<float>(0f);

        /// <summary>Sleep 상태 등에서 CatBrain이 조정 (docs/07 — 수면 민감도 30%).</summary>
        public float SensitivityMultiplier { get; set; } = 1f;
        /// <summary>성격 배율 (CatBrain이 스폰 시 설정 — design/cat-ideas/01).</summary>
        public float ViewMultiplier { get; set; } = 1f;
        public float HearingMultiplier { get; set; } = 1f;
        /// <summary>굴러가는 물건 반응 속도 기준 배율 (성격 — 호기심쟁이 0.5).</summary>
        public float CuriositySpeedMultiplier { get; set; } = 1f;

        // 시야 결과 (CatBrain이 읽음)
        public PlayerCondition VisibleTarget { get; private set; }
        public float ContinuousSightSeconds { get; private set; }
        public bool CloseSight { get; private set; }        // 2m 내 목격 — 즉시 추격
        public Vector3 LastStimulusPos { get; private set; }
        public bool HasNewStimulus { get; private set; }    // Suspicious 전이용 (Brain이 소비)
        public bool ImmediateInvestigate { get; private set; } // Break/Trap/Squeak (Brain이 소비)

        // 움직임 감지 (Brain이 읽음). 필터는 Brain이 준다 — 쿨다운·질림 목록
        public CarryableItem CuriosityTarget { get; private set; }
        public System.Func<CarryableItem, bool> CuriosityFilter { get; set; }
        /// <summary>이 물건 근처(1.5m)의 충돌 소음은 못 들은 척 — 자기가 친 물건에 매번 놀라지 않게. 깨짐은 예외.</summary>
        public Transform IgnoreImpactNear { get; set; }
        private CarryableItem[] _items;

        /// <summary>쥐별 감각 배율 (앙심 — CatBrain이 준다). 시야 게인·타깃 우선·발소리/찍찍 청각에 곱한다.</summary>
        public System.Func<ulong, float> TargetGainMultiplier { get; set; }
        /// <summary>문턱을 넘어 들린 소음 (기억·앙심 입력). heard = 배율 적용 후 크기.</summary>
        public event System.Action<NoiseEvent, float> Heard;

        // 잠시 못 본 척 (가지고 놀다 질린 쥐 — 놓아주자마자 다시 덮치지 않게)
        private readonly System.Collections.Generic.Dictionary<ulong, float> _ignoreUntil = new();
        public void IgnorePlayer(ulong clientId, float until) => _ignoreUntil[clientId] = until;
        /// <summary>청각 차단 (앙숙 싸움 — 서로에게 꽂혀 다른 소리가 묻힌다).</summary>
        public bool Deaf { get; set; }
        /// <summary>시야 거리·반각 덮어쓰기 (상자 안 — 정면 좁게, design/cat-ideas/09). null이면 기본.</summary>
        public float? ViewDistanceOverride { get; set; }
        public float? ViewHalfAngleOverride { get; set; }
        public bool IsIgnoring(ulong clientId) => _ignoreUntil.TryGetValue(clientId, out float t) && Time.time < t;
        private float _nextItemScan;

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

        /// <summary>호스트: 게이지를 최소 v로 (겁쟁이가 도망쳤다 돌아와 조사할 때 — 도망 3s 동안 게이지가 식어 바로 복귀하던 문제).</summary>
        public void RaiseGaugeTo(float v, Vector3 pos)
        {
            if (SuspicionGauge.Value < v) SuspicionGauge.Value = v;
            LastStimulusPos = pos;
            _lastStimulusTime = Time.time;
        }

        private void Update()
        {
            if (!IsSpawned) return; // 스폰 전 프레임 가드
            if (Time.time >= _nextVisionTick)
            {
                _nextVisionTick = Time.time + 0.2f;
                VisionTick(0.2f);
                MotionTick();
            }
            // 자극 없을 때 게이지 감쇠 (docs/07)
            if (VisibleTarget == null && Time.time - _lastStimulusTime > 0.5f && SuspicionGauge.Value > 0f)
                SuspicionGauge.Value = Mathf.Max(0f, SuspicionGauge.Value - _balance.CatGaugeDecayPerSec * Time.deltaTime);
        }

        private void VisionTick(float dt)
        {
            PlayerCondition seen = null;
            float bestDist = float.MaxValue;
            float bestCmp = float.MaxValue; // 찍힌 쥐는 가까이 있는 것처럼 비교 (우선 추적)
            float seenGain = 1f;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var playerObj = client.PlayerObject;
                if (playerObj == null) continue;
                var condition = playerObj.GetComponent<PlayerCondition>();
                if (condition == null || condition.State.Value != ConditionState.Active) continue;
                if (IsIgnoring(condition.OwnerClientId)) continue;

                Vector3 toPlayer = playerObj.transform.position - transform.position;
                float dist = toPlayer.magnitude;

                // 웅크림(동기화 스케일)이면 시야 거리 절반 (docs/07)
                bool crouching = playerObj.transform.localScale.y < Player.PlayerController.BaseScaleY * 0.75f;
                float viewDist = (ViewDistanceOverride ?? _balance.CatViewDistance) * (crouching ? _balance.CatCrouchViewMultiplier : 1f)
                                 * (World.LightZone.IsDark(playerObj.transform.position) ? _balance.CatDarkViewMultiplier : 1f)
                                 * SensitivityMultiplier * ViewMultiplier;
                if (dist > viewDist) continue;
                if (Vector3.Angle(transform.forward, toPlayer) > (ViewHalfAngleOverride ?? _balance.CatViewHalfAngle)) continue;

                int blockMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker", "Default");
                if (Physics.Linecast(transform.position + Vector3.up * 0.5f,
                        playerObj.transform.position, blockMask, QueryTriggerInteraction.Ignore)) continue;

                float gm = TargetGainMultiplier != null ? TargetGainMultiplier(condition.OwnerClientId) : 1f;
                float cmp = gm > 1f ? dist * 0.6f : dist;
                if (cmp < bestCmp) { bestCmp = cmp; bestDist = dist; seen = condition; seenGain = gm; }
            }

            if (seen != null)
            {
                bool sameTarget = seen == VisibleTarget;
                VisibleTarget = seen;
                ContinuousSightSeconds = sameTarget ? ContinuousSightSeconds + dt : dt;
                CloseSight = bestDist <= _balance.CatCloseSightDistance;
                // 거리보정: 가까울수록 큼 (1~2배)
                float distFactor = Mathf.Lerp(2f, 1f, bestDist / _balance.CatViewDistance);
                AddSuspicion(_balance.CatGazeGainPerSec * dt * distFactor * seenGain, seen.transform.position);
            }
            else
            {
                VisibleTarget = null;
                ContinuousSightSeconds = 0f;
                CloseSight = false;
            }
        }

        // 시야 안에서 빠르게 움직이는 풀린 물건 중 가장 가까운 것. 들린 것·주머니·대형은 제외 (쥐가 든 물건은 쥐 목격이 처리)
        private void MotionTick()
        {
            if (Time.time >= _nextItemScan)
            {
                _nextItemScan = Time.time + 2f;
                _items = FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            }
            CuriosityTarget = null;
            if (_items == null) return;
            float viewDist = _balance.CatViewDistance * SensitivityMultiplier * ViewMultiplier;
            float minSpeed = _balance.CatCuriosityMinSpeed * CuriositySpeedMultiplier;
            float best = float.MaxValue;
            Vector3 eye = transform.position + Vector3.up * 0.5f;
            int blockMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker", "Default");
            foreach (var item in _items)
            {
                if (item == null || !item.IsSpawned || item.IsHeavy || item.CarrierIds.Count > 0 || item.Pocketed.Value) continue;
                var rb = item.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic || rb.linearVelocity.sqrMagnitude < minSpeed * minSpeed) continue;
                Vector3 to = item.transform.position - transform.position;
                float dist = to.magnitude;
                if (dist > viewDist || dist >= best) continue;
                if (Vector3.Angle(transform.forward, to) > _balance.CatViewHalfAngle) continue;
                // 가려짐 검사 — 물건 자기 콜라이더에 맞는 건 보이는 것
                if (Physics.Linecast(eye, item.transform.position, out var hit, blockMask, QueryTriggerInteraction.Ignore)
                    && hit.rigidbody != rb) continue;
                if (CuriosityFilter != null && !CuriosityFilter(item)) continue;
                best = dist;
                CuriosityTarget = item;
            }
        }

        private void OnNoise(NoiseEvent e)
        {
            if (!IsServer || Deaf) return; // 싸움 중엔 못 듣는다 (design/cat-ideas/07)
            if (e.Loudness < NoiseSystem.MaskLoudness) return; // 청소기 소리에 묻힘 (design/cat-ideas/10)
            if (e.Type == NoiseType.Impact && IgnoreImpactNear != null
                && Vector3.Distance(e.Pos, IgnoreImpactNear.position) < 1.5f) return;
            float heard = NoiseSystem.GetLoudnessAt(e, transform.position) * SensitivityMultiplier * HearingMultiplier;
            // 찍힌 쥐의 발소리·찍찍은 더 잘 들린다 (충돌·파손은 Source가 비어 귀속 불가 — 제외)
            if (e.HasSource && (e.Type == NoiseType.Footstep || e.Type == NoiseType.Squeak) && TargetGainMultiplier != null)
            {
                float gm = TargetGainMultiplier(e.Source);
                if (gm > 1f) heard *= _balance.CatGrudgeHearingMul;
            }
            if (heard < _balance.CatHearThreshold) return;

            // 거리감쇠 (docs/07): 반경 대비 멀수록 약하게
            float radius = NoiseSystem.GetRadius(e);
            float dist = Vector3.Distance(e.Pos, transform.position);
            float distFactor = Mathf.Clamp(1f - dist / Mathf.Max(radius, 0.01f), 0.2f, 1f);
            AddSuspicion(heard * distFactor * _balance.CatHearingGain, e.Pos);
            Heard?.Invoke(e, heard);

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
