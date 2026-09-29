using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>고양이 순찰 — 스팟 고르기·도착 행동·머무름 (docs/07). CatBrain.cs에서 옮김 (고양이 203).</summary>
    public partial class CatBrain
    {
        private void TickPatrol()
        {
            if (CheckEscalation()) return;
            if (TryEatBribe()) return;    // 쥐가 바친 치즈 (design/cat-ideas/05)
            if (CheckScent()) return;     // 냄새 자국 (의심 아래, 호기심 위 — design/cat-ideas/06)
            if (CheckCuriosity()) return; // 순찰 중엔 굴러가는 물건에 속는다 (의심·추격 중엔 안 속음)

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

            if (_memoryVisit) { TickMemoryVisit(); return; }
            if (_spots.Length == 0) { _movement.Stop(); return; }
            if (_dwelling)
            {
                if (_goSit) FaceGoSit();
                if (Time.time < _waitUntil) return;
                _goSit = false;
                _dwelling = false;
                ExitPerch();
                _senses.SensitivityMultiplier = 1f;
                if (CurrentSpotType == CatSpotType.Groom && TryHairball()) return; // 그루밍 끝 — 가끔 헤어볼 (design/cat-ideas/11)
                if (TryStartZoomies()) return; // 화장실 끝 — 우다다 (design/cat-ideas/02)
                GoToNextSpot();
                return;
            }
            if (_goSit) { TickGoSit(); return; } // 스팟이 아닌 곳으로(TV·불 켜진 방) — 아래 "멈췄으면 스팟으로 다시"가 목적지를 덮지 않게
            if (_spotIndex < 0) { GoToNextSpot(); return; }
            if (_movement.Arrived) ArriveAtSpot();
            else if (_movement.Velocity.sqrMagnitude < 0.01f) _movement.MoveTo(_spots[_spotIndex].Pos, _balance.CatPatrolSpeed);
        }

        // 다음 스팟: 가중치 랜덤, 최근 n개 제외 (스팟이 적으면 제외 목록을 줄인다)
        private void GoToNextSpot()
        {
            if (IsFeeding) { int food = FindSpot(CatSpotType.Food); if (food >= 0) { _spotIndex = food; _movement.MoveTo(_spots[food].Pos, _balance.CatPatrolSpeed); return; } }
            if (TryRouteNext()) return; // 순찰꾼은 큰길을 오간다 (고양이 92)
            if (TryFinaleHoleVisit()) return; // 귀환 카운트다운 — 쥐구멍 쪽 (design/cat-ideas/12)
            if (TryGoToMemorySpot()) return; // 가끔 기억 칸에 들른다 (design/cat-ideas/05)
            int avoid = Mathf.Min(_balance.CatSpotAvoidRecent, (_isGuard ? GuardEligibleCount() : _spots.Length) - 1); // 문지기는 고를 스팟이 적어 최근 제외가 전부 막지 않게
            while (_recentSpots.Count > avoid) _recentSpots.RemoveAt(0);
            float total = 0f;
            for (int i = 0; i < _spots.Length; i++) if (!_recentSpots.Contains(i)) total += SpotWeight(i);
            if (total <= 0f) { _movement.Stop(); return; } // 순찰할 스팟이 없음 (문뿐)
            float r = Random.value * total;
            int pick = -1;
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_recentSpots.Contains(i) || SpotWeight(i) <= 0f) continue;
                r -= SpotWeight(i);
                if (r <= 0f) { pick = i; break; }
            }
            if (pick < 0) pick = 0;
            _spotIndex = pick;
            _recentSpots.Add(pick);
            _movement.MoveTo(_spots[pick].Pos, _balance.CatPatrolSpeed);
            AnnounceNextSpot(_spots[pick].Type); // 루틴 예고 — 소리로 읽힌다 (design/cat-ideas/02)
        }

        // 성격이 잠자리·관찰점 선호를 바꾼다 (design/cat-ideas/01)
        private float SpotWeight(int i)
        {
            if (_spots[i].Type == CatSpotType.Door) return 0f; // 문은 순찰 대상 아님
            float w = _spots[i].Weight * DirSpotWeightMul(_spots[i].Type); // 디렉터 Relief면 루틴 스팟 쪽으로
            var p = Personality;
            if (p != null)
                w = _spots[i].Type switch
                {
                    CatSpotType.Bed => w * p.BedWeightMultiplier,
                    CatSpotType.Look => w * p.LookWeightMultiplier,
                    CatSpotType.Perch => w * p.PerchWeightMultiplier,
                    _ => w
                };
            return GuardFilter(i, w); // 문지기는 초소 둘레만 (고양이 80)
        }

        // 스팟 종류별 머무름: 잠자리는 Sleep 상태로, 나머지는 시간+감각 배율 (design/cat-design/02 카탈로그)
        private void ArriveAtSpot()
        {
            var spot = _spots[_spotIndex];
            // 청소기 피신 중이면 스팟 종류와 상관없이 끝날 때까지 웅크림 (design/cat-ideas/10)
            if (AvoidingVacuum) { Dwell(_avoidVacuumUntil - Time.time, _balance.VacuumCatSense); return; }
            // 복귀(NearestSpot)로 도착한 스팟도 "방금 쓴 스팟"으로 — 안 그러면 복귀 → 같은 매복 스팟 → 또 매복이 반복된다 (고양이 21 소크)
            if (!_recentSpots.Contains(_spotIndex)) _recentSpots.Add(_spotIndex);
            Log.Dev($"고양이 [{name}]: 스팟 도착 {spot.Name} ({spot.Type})");
            switch (spot.Type)
            {
                case CatSpotType.Bed:
                    SetState(CatState.Sleep);
                    return;
                case CatSpotType.Ambush:
                    EnterAmbush(); return;   // 커튼 뒤 매복 (design/cat-ideas/09)
                case CatSpotType.Box:
                    EnterBoxSit(); return;   // 상자 입구에 앉기
                case CatSpotType.Food:
                    // 밥 시간이면 남은 시간만큼 먹는다 (집주인 이벤트)
                    Dwell(IsFeeding ? _feedingUntil - Time.time : _balance.CatSpotFoodSeconds * DirRoutineDwellMul, _balance.CatSpotFoodSense); return;
                case CatSpotType.Sun:
                    Dwell(_balance.CatSpotSunSeconds * DirRoutineDwellMul, _balance.CatSpotSunSense); return;
                case CatSpotType.Groom:
                    Dwell(_balance.CatSpotGroomSeconds * DirRoutineDwellMul, _balance.CatSpotGroomSense); return;
                case CatSpotType.Litter:
                    _litterDone = true; // 볼일 끝나면 우다다
                    Dwell(_balance.CatLitterSeconds, _balance.CatLitterSense); return;
                case CatSpotType.Water:
                    Dwell(_balance.CatWaterSeconds, 1f); return;
                case CatSpotType.Perch:
                    EnterPerch(); return;    // 선반 위에서 내려다보기 (고양이 39)
                default:
                    Dwell(Random.Range(_balance.CatPatrolWaitRange.x, _balance.CatPatrolWaitRange.y) * LookDwellMul * DirLookDwellMul, 1f); return;
            }
        }

        private void Dwell(float seconds, float sense)
        {
            _dwelling = true;
            _waitUntil = Time.time + seconds;
            _senses.SensitivityMultiplier = sense;
            _movement.Stop();
        }
    }
}
