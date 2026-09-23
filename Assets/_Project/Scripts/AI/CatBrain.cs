using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    public enum CatState { Sleep, Patrol, Suspicious, Chase, Capture, Distracted, Return }

    /// <summary>
    /// 고양이 FSM (docs/07, 호스트에서만 Update). 클라는 NetworkTransform 보간 + 상태 연출만.
    /// Distracted(유인 아이템)는 태스크 1-6에서 트리거가 생긴다 — 진입 API만 준비.
    /// 애니·사운드 연출 계약(CatAnimatorLink)은 아트 단계.
    /// </summary>
    public class CatBrain : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<CatState> State = new NetworkVariable<CatState>(CatState.Patrol);
        public NetworkVariable<ulong> TargetClientId = new NetworkVariable<ulong>(0);

        private CatSenses _senses;
        private CatMovement _movement;

        // 스팟 그래프 (design/cat-design/02): CatSpot 컴포넌트가 없으면 씬의 CatWaypoint* 를 Look 스팟으로 쓴다
        private struct Spot { public Vector3 Pos; public CatSpotType Type; public float Weight; public string Name; }
        private Spot[] _spots;
        private int _spotIndex = -1;
        private readonly System.Collections.Generic.List<int> _recentSpots = new();
        private bool _dwelling;                 // 스팟에 도착해 머무는 중
        private float _waitUntil;               // Patrol 대기(스팟 머무름 끝)
        private float _sleepUntil;              // Bed 잠 끝
        public CatSpotType CurrentSpotType => _spotIndex >= 0 && _spots != null ? _spots[_spotIndex].Type : CatSpotType.Look;
        public string CurrentSpotName => _spotIndex >= 0 && _spots != null ? _spots[_spotIndex].Name : "";
        private Vector3 _investigatePos;
        private float _suspiciousUntil;
        private PlayerCondition _chaseTarget;
        private Vector3 _lastKnownTargetPos; // 시야 있을 때만 갱신 — 월핵 추적 금지 (docs/07)
        private float _lostSightSince = -1f;
        private float _captureSwingEnd;
        private float _groomUntil;
        private Vector3 _distractPos;
        private float _distractUntil;
        private CatState _stateBeforeDistract;

        private void Awake()
        {
            _senses = GetComponent<CatSenses>();
            _movement = GetComponent<CatMovement>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                _movement.SetEnabled(false);
                return;
            }
            CollectSpots();
            SetState(CatState.Patrol);
        }

        // 씬의 CatSpot 전부 (방 모듈 단계에서는 존 그래프로 — 지금은 씬 = 방 1개). 없으면 CatWaypoint* 이름을 Look으로
        private void CollectSpots()
        {
            var list = new System.Collections.Generic.List<Spot>();
            foreach (var spot in GameObject.FindObjectsByType<CatSpot>(FindObjectsSortMode.None))
                list.Add(new Spot { Pos = spot.transform.position, Type = spot.Type, Weight = spot.Weight, Name = spot.name });
            if (list.Count == 0)
                foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.StartsWith("CatWaypoint"))
                        list.Add(new Spot { Pos = t.position, Type = CatSpotType.Look, Weight = 1f, Name = t.name });
            _spots = list.ToArray();
            Log.Dev($"고양이 [{name}]: 스팟 {_spots.Length}개");
        }

        private Vector3? _tempWaypoint; // 쿠키 부스러기 — 1회 경유 (docs/07)

        /// <summary>쿠키 부스러기: 순찰 경로에 임시 삽입, 1회 경유 (docs/07·08).</summary>
        public void ServerInsertWaypoint(Vector3 pos)
        {
            if (!IsServer) return;
            _tempWaypoint = pos;
            Log.Dev($"고양이 [{name}]: 임시 웨이포인트 삽입 {pos:F1}");
        }

        /// <summary>유인 아이템 진입점 (docs/07 — Chase보다 우선순위 낮음). 태스크 1-6에서 호출.</summary>
        public void ServerDistract(Vector3 pos, float seconds)
        {
            if (!IsServer || State.Value == CatState.Chase || State.Value == CatState.Capture) return;
            _distractPos = pos;
            _distractUntil = Time.time + seconds;
            _stateBeforeDistract = State.Value == CatState.Distracted ? _stateBeforeDistract : State.Value;
            SetState(CatState.Distracted);
        }

        private void Update()
        {
            if (!IsSpawned || _spots == null) return; // 자동 부트 등 스폰 전 프레임 가드
            switch (State.Value)
            {
                case CatState.Sleep: TickSleep(); break;
                case CatState.Patrol: TickPatrol(); break;
                case CatState.Suspicious: TickSuspicious(); break;
                case CatState.Chase: TickChase(); break;
                case CatState.Capture: TickCapture(); break;
                case CatState.Distracted: TickDistracted(); break;
                case CatState.Return: TickReturn(); break;
            }
        }

        private void SetState(CatState next)
        {
            if (State.Value == next) return;
            Log.Dev($"고양이 [{name}]: {State.Value} → {next}");
            State.Value = next;
            _senses.SensitivityMultiplier = next == CatState.Sleep ? _balance.CatSleepSenseMultiplier : 1f;
            if (next != CatState.Patrol) _dwelling = false;

            switch (next)
            {
                case CatState.Sleep: _movement.Stop(); _sleepUntil = Time.time + _balance.CatBedSleepSeconds; break;
                case CatState.Patrol: _waitUntil = 0f; _dwelling = false; break;
                case CatState.Suspicious:
                    _suspiciousUntil = Time.time + _balance.CatSuspiciousWanderSeconds;
                    _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                    break;
                case CatState.Chase:
                    TargetClientId.Value = _chaseTarget != null ? _chaseTarget.OwnerClientId : 0;
                    if (_chaseTarget != null) _lastKnownTargetPos = _chaseTarget.transform.position;
                    _lostSightSince = -1f;
                    break;
                case CatState.Capture:
                    _movement.Stop();
                    _captureSwingEnd = Time.time + _balance.CatCaptureSwingSeconds;
                    _groomUntil = 0f;
                    break;
                case CatState.Return:
                    TargetClientId.Value = 0;
                    if (_spots.Length > 0)
                        _movement.MoveTo(NearestSpot(), _balance.CatReturnSpeed);
                    break;
                case CatState.Distracted:
                    _movement.MoveTo(_distractPos, _balance.CatDistractedSpeed);
                    break;
            }
        }

        // ---- 상태별 틱 ----

        private void TickSleep()
        {
            // 소음 ≥40 근접(자극 지점이 가까움) → Suspicious (docs/07 FSM)
            if (_senses.HasNewStimulus && _senses.SuspicionGauge.Value >= _balance.CatSuspicionThreshold)
            {
                EnterSuspicious();
                return;
            }
            if (Time.time >= _sleepUntil)
            {
                // 다 잤다 — 아직 잠자리 위라 Arrived가 참이므로, 같은 스팟에 다시 "도착"하지 않게 다음 스팟을 바로 고른다
                SetState(CatState.Patrol);
                GoToNextSpot();
            }
        }

        private void TickPatrol()
        {
            if (CheckEscalation()) return;

            // 임시 웨이포인트(쿠키) 우선 — 도착하면 소비
            if (_tempWaypoint.HasValue)
            {
                _movement.MoveTo(_tempWaypoint.Value, _balance.CatPatrolSpeed);
                if (Vector3.Distance(transform.position, _tempWaypoint.Value) < 1f)
                {
                    _tempWaypoint = null;
                    _waitUntil = Time.time + Random.Range(_balance.CatPatrolWaitRange.x, _balance.CatPatrolWaitRange.y);
                }
                return;
            }

            if (_spots.Length == 0) { _movement.Stop(); return; }
            if (_dwelling)
            {
                if (Time.time < _waitUntil) return;
                _dwelling = false;
                _senses.SensitivityMultiplier = 1f;
                GoToNextSpot();
                return;
            }
            if (_spotIndex < 0) { GoToNextSpot(); return; }
            if (_movement.Arrived) ArriveAtSpot();
            else if (_movement.Velocity.sqrMagnitude < 0.01f) _movement.MoveTo(_spots[_spotIndex].Pos, _balance.CatPatrolSpeed);
        }

        // 다음 스팟: 가중치 랜덤, 최근 n개 제외 (스팟이 적으면 제외 목록을 줄인다)
        private void GoToNextSpot()
        {
            int avoid = Mathf.Min(_balance.CatSpotAvoidRecent, _spots.Length - 1);
            while (_recentSpots.Count > avoid) _recentSpots.RemoveAt(0);
            float total = 0f;
            for (int i = 0; i < _spots.Length; i++) if (!_recentSpots.Contains(i)) total += _spots[i].Weight;
            float r = Random.value * total;
            int pick = -1;
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_recentSpots.Contains(i)) continue;
                r -= _spots[i].Weight;
                if (r <= 0f) { pick = i; break; }
            }
            if (pick < 0) pick = 0;
            _spotIndex = pick;
            _recentSpots.Add(pick);
            _movement.MoveTo(_spots[pick].Pos, _balance.CatPatrolSpeed);
        }

        // 스팟 종류별 머무름: 잠자리는 Sleep 상태로, 나머지는 시간+감각 배율 (design/cat-design/02 카탈로그)
        private void ArriveAtSpot()
        {
            var spot = _spots[_spotIndex];
            Log.Dev($"고양이 [{name}]: 스팟 도착 {spot.Name} ({spot.Type})");
            switch (spot.Type)
            {
                case CatSpotType.Bed:
                    SetState(CatState.Sleep);
                    return;
                case CatSpotType.Food:
                    Dwell(_balance.CatSpotFoodSeconds, _balance.CatSpotFoodSense); return;
                case CatSpotType.Sun:
                    Dwell(_balance.CatSpotSunSeconds, _balance.CatSpotSunSense); return;
                case CatSpotType.Groom:
                    Dwell(_balance.CatSpotGroomSeconds, _balance.CatSpotGroomSense); return;
                default:
                    Dwell(Random.Range(_balance.CatPatrolWaitRange.x, _balance.CatPatrolWaitRange.y), 1f); return;
            }
        }

        private void Dwell(float seconds, float sense)
        {
            _dwelling = true;
            _waitUntil = Time.time + seconds;
            _senses.SensitivityMultiplier = sense;
            _movement.Stop();
        }

        private void TickSuspicious()
        {
            if (CheckEscalation()) return;

            // 새 자극이 오면 조사 지점 갱신 + 시간 연장
            if (_senses.HasNewStimulus)
            {
                _investigatePos = _senses.LastStimulusPos;
                _suspiciousUntil = Time.time + _balance.CatSuspiciousWanderSeconds;
                _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                _senses.ConsumeStimulus();
            }
            if (_movement.Arrived) // 주변 3m 배회 (docs/07)
                _movement.MoveTo(_movement.RandomPointAround(_investigatePos, 3f), _balance.CatSuspiciousSpeed);

            if (_senses.SuspicionGauge.Value <= 0f || Time.time >= _suspiciousUntil)
                SetState(CatState.Return);
        }

        private void TickChase()
        {
            if (_chaseTarget == null || _chaseTarget.State.Value != ConditionState.Active)
            {
                SetState(CatState.Return);
                return;
            }

            bool seeing = _senses.VisibleTarget == _chaseTarget;
            if (seeing)
            {
                _lostSightSince = -1f;
                _lastKnownTargetPos = _chaseTarget.transform.position;
                _movement.MoveTo(_lastKnownTargetPos, _balance.CatChaseSpeed);
                if (Vector3.Distance(transform.position, _lastKnownTargetPos) <= _balance.CatCaptureRange)
                    SetState(CatState.Capture);
            }
            else
            {
                if (_lostSightSince < 0f) _lostSightSince = Time.time;
                _movement.MoveTo(_lastKnownTargetPos, _balance.CatChaseSpeed); // 마지막 "목격" 지점만 (월핵 금지)
                if (Time.time - _lostSightSince >= _balance.CatLoseSightSeconds)
                {
                    _investigatePos = _lastKnownTargetPos;
                    _chaseTarget = null;
                    _senses.ConsumeStimulus();
                    SetState(CatState.Suspicious); // 마지막 목격점 조사 (docs/07)
                    _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                }
            }
        }

        private void TickCapture()
        {
            // 0.4s 스윙 → 전방 1.2m 판정 (애니 종료 시점 위치 기준 — 회피 가능, docs/07)
            if (_groomUntil > 0f)
            {
                if (Time.time >= _groomUntil) SetState(CatState.Return);
                return;
            }
            if (Time.time < _captureSwingEnd) return;

            foreach (var col in Physics.OverlapSphere(
                transform.position + transform.forward * 0.6f, _balance.CatCaptureRadius,
                LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
            {
                var condition = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetComponent<PlayerCondition>() : null;
                if (condition != null && condition.State.Value == ConditionState.Active)
                {
                    condition.ServerSetState(ConditionState.Downed);
                    Log.Dev($"고양이 [{name}] 포획: client {condition.OwnerClientId}");
                    _groomUntil = Time.time + _balance.CatGroomSeconds; // 그루밍(승리 연출)
                    return;
                }
            }
            // 헛스윙 — 추격 재개
            SetState(CatState.Chase);
        }

        private void TickDistracted()
        {
            if (Time.time >= _distractUntil)
            {
                SetState(_stateBeforeDistract == CatState.Distracted ? CatState.Return : _stateBeforeDistract);
                return;
            }
            if (_movement.Arrived)
                _movement.MoveTo(_movement.RandomPointAround(_distractPos, 1.5f), _balance.CatDistractedSpeed);
        }

        private void TickReturn()
        {
            if (CheckEscalation()) return;
            if (_spots.Length == 0 || _movement.Arrived) SetState(CatState.Patrol);
        }

        // ---- 공통 전이 ----

        private bool CheckEscalation()
        {
            // 직접목격 1s or 2m 내 목격 or 게이지 만땅 → Chase (docs/07)
            if (_senses.VisibleTarget != null &&
                (_senses.ContinuousSightSeconds >= _balance.CatDirectSightChaseSeconds
                 || _senses.CloseSight
                 || _senses.SuspicionGauge.Value >= _balance.CatChaseThreshold))
            {
                _chaseTarget = _senses.VisibleTarget;
                SetState(CatState.Chase);
                return true;
            }
            if ((_senses.ImmediateInvestigate
                 || _senses.SuspicionGauge.Value >= _balance.CatSuspicionThreshold) && _senses.HasNewStimulus)
            {
                EnterSuspicious();
                return true;
            }
            return false;
        }

        private void EnterSuspicious()
        {
            _investigatePos = _senses.LastStimulusPos;
            _senses.ConsumeStimulus();
            SetState(CatState.Suspicious);
        }

        // 복귀는 가장 가까운 스팟으로 — 도착하면 Patrol이 그 스팟 행동부터 이어 간다
        private Vector3 NearestSpot()
        {
            Vector3 best = transform.position;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _spots.Length; i++)
            {
                float d = Vector3.Distance(transform.position, _spots[i].Pos);
                if (d < bestDist) { bestDist = d; best = _spots[i].Pos; _spotIndex = i; }
            }
            _dwelling = false;
            return best;
        }
    }
}
