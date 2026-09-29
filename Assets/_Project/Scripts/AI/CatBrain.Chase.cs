using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>고양이 추격·포획 (docs/07). CatBrain.cs에서 옮김 (고양이 203).</summary>
    public partial class CatBrain
    {
        private void TickChase()
        {
            // 숨은(Hidden) 타깃은 "놓친 것"으로 다룬다 — 시야 상실 분기를 거쳐 수색으로 (design/cat-ideas/14)
            if (_chaseTarget == null || (_chaseTarget.State.Value != ConditionState.Active && _chaseTarget.State.Value != ConditionState.Hidden))
            {
                SetState(CatState.Return);
                return;
            }

            // 쫓던 쥐가 안전지대(목적지방)로 들어가면 포기 — 거기엔 못 들어간다 (고양이 72)
            if (World.SafeZone.Contains(_chaseTarget.transform.position))
            {
                Log.Dev($"고양이 [{name}]: 안전지대로 도망침 — 포기");
                _chaseTarget = null;
                _senses.ConsumeStimulus();
                SetState(CatState.Return);
                return;
            }

            // 게으름뱅이: 오래 쫓으면 하품하고 포기 (design/cat-ideas/01)
            float giveUp = ChaseGiveUp > 0f && IsGrudged(_chaseTarget.OwnerClientId) ? ChaseGiveUp + _balance.CatGrudgeGiveUpBonus : ChaseGiveUp;
            if (giveUp > 0f && Time.time - _chaseStartedAt >= giveUp)
            {
                Log.Dev($"고양이 [{name}]: 추격 포기 (성격 {Personality.DisplayName}, {giveUp}s)");
                ServerAddGrudge(_chaseTarget.OwnerClientId, _balance.CatGrudgeEscape, "놓침");
                _chaseTarget = null;
                _senses.ConsumeStimulus();
                SetState(CatState.Return);
                return;
            }

            // 코너 감속: 몸이 경로 방향과 어긋날수록 느리게 — 직선은 최고 속도, 지그재그는 실제로 도움된다 (design/cat-design/01-6)
            float align = Mathf.InverseLerp(-0.2f, 0.9f, _movement.SteeringAlignment);
            float chaseSpeed = _balance.CatChaseSpeed * ChaseSpeedMul * Mathf.Lerp(_balance.CatChaseCornerSpeedMul, 1f, align);

            bool seeing = _senses.VisibleTarget == _chaseTarget;
            if (seeing)
            {
                Vector3 pos = _chaseTarget.transform.position;
                // 타깃 속도(위치 차분) — 첫 프레임은 0
                _targetVelocity = _lostSightSince < 0f && _targetPrevPos != Vector3.zero ? (pos - _targetPrevPos) / Mathf.Max(Time.deltaTime, 0.001f) : Vector3.zero;
                _targetPrevPos = pos;
                _lostSightSince = -1f;
                _overshooting = false;
                _lastKnownTargetPos = pos;
                // 예측 추격: 타깃이 가는 방향 1s 앞을 노린다. NavMesh 밖이면 타깃 위치로
                Vector3 lead = pos + Vector3.ClampMagnitude(_targetVelocity, _balance.CatChaseSpeed) * _balance.CatChaseLeadSeconds;
                _movement.MoveTo(CatMovement.Sample(lead, 1.5f, pos), chaseSpeed);
                if (Vector3.Distance(transform.position, pos) <= _balance.CatCaptureRange)
                    SetState(CatState.Capture);
            }
            else
            {
                if (_lostSightSince < 0f)
                {
                    _lostSightSince = Time.time;
                    _targetPrevPos = Vector3.zero;
                    // 오버슛: 마지막 진행 방향으로 3m 더 — 코너를 돌아 "따라 도는" 느낌. NavMesh 밖이면 마지막 목격점
                    Vector3 dir = _targetVelocity; dir.y = 0f;
                    _overshooting = dir.sqrMagnitude > 0.25f;
                    Vector3 goal = _overshooting ? _lastKnownTargetPos + dir.normalized * _balance.CatChaseOvershootMeters : _lastKnownTargetPos;
                    _movement.MoveTo(CatMovement.Sample(goal, 1.5f, _lastKnownTargetPos), chaseSpeed);
                }
                else if (_overshooting && _movement.Arrived)
                {
                    _overshooting = false;
                    _movement.MoveTo(_lastKnownTargetPos, chaseSpeed); // 오버슛 끝 — 마지막 "목격" 지점만 (월핵 금지)
                }
                if (Time.time - _lostSightSince >= _balance.CatLoseSightSeconds)
                {
                    _investigatePos = _lastKnownTargetPos;
                    ServerAddGrudge(_chaseTarget.OwnerClientId, _balance.CatGrudgeEscape, "놓침");
                    _chaseTarget = null;
                    _senses.ConsumeStimulus();
                    // 마지막 목격점 근처에 숨을 곳이 있으면 수색, 없으면 조사 (docs/07)
                    if (TryEnterSearch(_lastKnownTargetPos)) return;
                    SetState(CatState.Suspicious);
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
                    if (IsKitten) { KittenKnockdown(condition); return; } // 아기는 못 잡는다 — 넘어뜨리기만 (design/cat-ideas/07)
                    if (TryStartToy(condition)) return; // 동료가 있으면 바로 끝내지 않고 가지고 논다 (design/cat-ideas/04)
                    condition.ServerSetState(ConditionState.Downed);
                    Log.Dev($"고양이 [{name}] 포획: client {condition.OwnerClientId}");
                    _groomUntil = Time.time + _balance.CatGroomSeconds; // 그루밍(승리 연출)
                    return;
                }
            }
            // 헛스윙 — 추격 재개
            SetState(CatState.Chase);
        }
    }
}
