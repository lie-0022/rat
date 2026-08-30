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

        private Transform[] _waypoints;
        private int _waypointIndex;
        private float _waitUntil;               // Patrol 대기
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
            // 순찰 웨이포인트: 씬의 CatWaypoint* (방 모듈 단계에서 RoomModule 소유로 이관 예정)
            var found = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in found) if (t.name.StartsWith("CatWaypoint")) list.Add(t);
            _waypoints = list.ToArray();
            SetState(CatState.Patrol);
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

            switch (next)
            {
                case CatState.Sleep: _movement.Stop(); break;
                case CatState.Patrol: _waitUntil = 0f; break;
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
                    if (_waypoints.Length > 0)
                        _movement.MoveTo(NearestWaypoint(), _balance.CatReturnSpeed);
                    break;
                case CatState.Distracted:
                    _movement.MoveTo(_distractPos, 4f);
                    break;
            }
        }

        // ---- 상태별 틱 ----

        private void TickSleep()
        {
            // 소음 ≥40 근접(자극 지점이 가까움) → Suspicious (docs/07 FSM)
            if (_senses.HasNewStimulus && _senses.SuspicionGauge.Value >= _balance.CatSuspicionThreshold)
                EnterSuspicious();
        }

        private void TickPatrol()
        {
            if (CheckEscalation()) return;

            if (_waypoints.Length == 0) { _movement.Stop(); return; }
            if (Time.time < _waitUntil) return;
            if (_movement.Arrived)
            {
                _waitUntil = Time.time + Random.Range(_balance.CatPatrolWaitRange.x, _balance.CatPatrolWaitRange.y);
                _waypointIndex = (_waypointIndex + 1) % _waypoints.Length;
                _movement.MoveTo(_waypoints[_waypointIndex].position, _balance.CatPatrolSpeed);
            }
            else if (_movement.Velocity.sqrMagnitude < 0.01f)
            {
                _movement.MoveTo(_waypoints[_waypointIndex].position, _balance.CatPatrolSpeed);
            }
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
                _movement.MoveTo(_movement.RandomPointAround(_distractPos, 1.5f), 4f);
        }

        private void TickReturn()
        {
            if (CheckEscalation()) return;
            if (_waypoints.Length == 0 || _movement.Arrived) SetState(CatState.Patrol);
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

        private Vector3 NearestWaypoint()
        {
            Vector3 best = transform.position;
            float bestDist = float.MaxValue;
            foreach (var w in _waypoints)
            {
                float d = Vector3.Distance(transform.position, w.position);
                if (d < bestDist) { bestDist = d; best = w.position; _waypointIndex = System.Array.IndexOf(_waypoints, w); }
            }
            return best;
        }
    }
}
