using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>고양이 의심·유인·복귀와 공통 전이(격상·가장 가까운 스팟) (docs/07). CatBrain.cs에서 옮김 (고양이 203).</summary>
    public partial class CatBrain
    {
        private void TickSuspicious()
        {
            if (CheckEscalation()) return;
            if (TryEatBribe()) return;
            if (Personality != null && Personality.CuriousWhileSuspicious && CheckCuriosity()) return; // 호기심쟁이는 의심 중에도 속는다

            // 새 자극이 오면 조사 지점 갱신 + 시간 연장
            if (_senses.HasNewStimulus)
            {
                _investigatePos = _senses.LastStimulusPos;
                _suspiciousArrived = false;
                _suspiciousUntil = Time.time + _balance.CatSuspiciousTravelSeconds;
                _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                _senses.ConsumeStimulus();
            }
            if (_movement.Arrived) // 주변 3m 배회 (docs/07)
            {
                if (!_suspiciousArrived)
                {
                    _suspiciousArrived = true;
                    _suspiciousUntil = Time.time + _balance.CatSuspiciousWanderSeconds;
                }
                _movement.MoveTo(_movement.RandomPointAround(_investigatePos, 3f), _balance.CatSuspiciousSpeed);
            }

            // 게이지가 식어도 조사는 끝까지 — 먼 곳의 깨짐은 게이지가 조금만 올라 도착 전에 식어 버렸다 (고양이 44 수용 기준)
            if (Time.time >= _suspiciousUntil)
                SetState(CatState.Return);
        }

        private void TickDistracted()
        {
            if (Time.time >= _distractUntil)
            {
                FinishDistractExtras(); // 뇌물 먹기 끝·속았음 깨닫기
                if (_wobbleAfterDistract)
                {
                    _wobbleAfterDistract = false;
                    Log.Dev($"고양이 [{name}]: 캣닢 끝 — 비틀비틀");
                    EnterBlunder(CatBlunderKind.Wobble, _balance.CatWobbleSeconds);
                    return;
                }
                SetState(_stateBeforeDistract == CatState.Distracted ? CatState.Return : _stateBeforeDistract);
                return;
            }
            if (TickBribe()) return; // 뇌물은 그 자리에서 먹는다
            if (_movement.Arrived)
                _movement.MoveTo(_movement.RandomPointAround(_distractPos, 1.5f), _balance.CatDistractedSpeed);
        }

        private void TickReturn()
        {
            if (CheckEscalation()) return;
            if (TryEatBribe()) return;
            if (CheckScent()) return;
            if (CheckCuriosity()) return;
            if (_spots.Length == 0 || _movement.Arrived) SetState(CatState.Patrol);
        }

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

        // 복귀는 가장 가까운 스팟으로 — 도착하면 Patrol이 그 스팟 행동부터 이어 간다. 방금 자고 일어난 잠자리는 제외(다시 눕지 않게)
        private Vector3 NearestSpot()
        {
            Vector3 best = transform.position;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _spots.Length; i++)
            {
                // 방금 쓴 "행동 스팟"(잠자리·매복·상자·화장실)으로는 복귀하지 않는다 — 도착하면 그 행동을 또 해서
                // 매복 60s → 복귀 → 같은 매복 스팟 → 또 매복… 무한 반복되던 문제 (고양이 20 소크 테스트)
                bool activitySpot = _spots[i].Type is CatSpotType.Bed or CatSpotType.Ambush or CatSpotType.Box or CatSpotType.Litter;
                if (activitySpot && _recentSpots.Contains(i) && _spots.Length > 1) continue;
                if (_spots[i].Type == CatSpotType.Door) continue; // 문은 복귀 지점이 아니다
                float d = Vector3.Distance(transform.position, _spots[i].Pos);
                if (d < bestDist) { bestDist = d; best = _spots[i].Pos; _spotIndex = i; }
            }
            _dwelling = false;
            return best;
        }
    }
}
