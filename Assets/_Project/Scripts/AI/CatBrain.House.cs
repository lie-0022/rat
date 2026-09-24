using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 집주인 이벤트에 대한 고양이 반응 (design/cat-ideas/10, 2026-09-24). 사건 자체는 Run/HouseEventDirector가 정한다.
    ///  부르기: 하던 일(추격 포함 — 간식이 더 중요)을 멈추고 가까운 문으로 → 사라짐 → 시간 뒤 **랜덤 문**으로 등장 → Return.
    ///         놀이·포획 중이면 끝난 뒤에 간다.
    ///  밥 시간: Food 스팟으로 가서 남은 시간만큼 먹는다(머무름 감각 0.5). 목격 전이는 그대로 — 부엌만 위험.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>문 밖으로 나가 안 보이는 중 (CatVisual이 렌더러를 끈다).</summary>
        public NetworkVariable<bool> AwayHidden = new NetworkVariable<bool>(false);

        private float _awaySeconds;
        private float _awayUntil;
        private float _awayWalkDeadline;
        private bool _callAwayPending;
        private float _feedingUntil;

        public bool IsFeeding => Time.time < _feedingUntil;

        /// <summary>호스트: 집주인이 불렀다. seconds = 밖에 있는 시간.</summary>
        public void ServerCallAway(float seconds)
        {
            if (!IsServer) return;
            _awaySeconds = seconds;
            if (State.Value == CatState.Toy || State.Value == CatState.Capture) { _callAwayPending = true; return; }
            SetState(CatState.Away);
        }

        /// <summary>호스트: 밥 시간. 추격·놀이·부재 중이면 무시.</summary>
        public void ServerFeedingTime(float seconds)
        {
            if (!IsServer) return;
            var st = State.Value;
            if (st == CatState.Chase || st == CatState.Capture || st == CatState.Toy || st == CatState.Away) return;
            int food = FindSpot(CatSpotType.Food);
            if (food < 0) return;
            _feedingUntil = Time.time + seconds;
            SetState(CatState.Patrol);
            CancelMemoryVisit();
            _dwelling = false;
            _spotIndex = food;
            _movement.MoveTo(_spots[food].Pos, _balance.CatPatrolSpeed * 1.5f); // 밥이다 — 서두른다
            Log.Dev($"고양이 [{name}]: 밥 시간 — {_spots[food].Name}로 ({seconds}s)");
        }

        /// <summary>호스트: 로봇청소기가 켜졌다 — 청소기에서 가장 먼 스팟으로 가서 끝날 때까지 웅크림.</summary>
        public void ServerAvoidVacuum(Vector3 vacuumPos, float seconds)
        {
            if (!IsServer) return;
            var st = State.Value;
            if (st is CatState.Chase or CatState.Capture or CatState.Toy or CatState.Away or CatState.Fight) return;
            int best = -1; float bestD = -1f;
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_spots[i].Type == CatSpotType.Door) continue;
                float d = Vector3.Distance(_spots[i].Pos, vacuumPos);
                if (d > bestD) { bestD = d; best = i; }
            }
            if (best < 0) return;
            _avoidVacuumUntil = Time.time + seconds;
            SetState(CatState.Patrol);
            CancelMemoryVisit();
            _dwelling = false;
            _spotIndex = best;
            _movement.MoveTo(_spots[best].Pos, _balance.CatPatrolSpeed * 1.5f);
            Log.Dev($"고양이 [{name}]: 청소기 싫어 — {_spots[best].Name}로 피신 ({seconds}s)");
        }

        private float _avoidVacuumUntil;

        private bool _watchingTv;
        private float _watchTvUntil;
        private Vector3 _tvPos;
        private Vector3 _tvWatchPoint;

        /// <summary>호스트: TV가 켜졌다 — TV 앞 시청 지점으로 가서 끝날 때까지 화면만 본다.</summary>
        public void ServerWatchTv(Vector3 tvPos, Vector3 watchPoint, float seconds)
        {
            if (!IsServer) return;
            var st = State.Value;
            if (st is CatState.Chase or CatState.Capture or CatState.Toy or CatState.Away or CatState.Fight) return;
            _tvPos = tvPos;
            _watchTvUntil = Time.time + seconds;
            SetState(CatState.Patrol);
            CancelMemoryVisit();
            _dwelling = false;
            _watchingTv = true;
            _tvWatchPoint = CatMovement.Sample(watchPoint, 1.5f, watchPoint);
            _movement.MoveTo(_tvWatchPoint, _balance.CatPatrolSpeed);
            Log.Dev($"고양이 [{name}]: TV 보러 ({seconds}s)");
        }

        // TickPatrol에서 (TV 보러 가는 중, 아직 안 앉음)
        private void TickWalkToTv()
        {
            if (Time.time >= _watchTvUntil) { _watchingTv = false; GoToNextSpot(); return; }
            if (Vector3.Distance(transform.position, _tvWatchPoint) <= 0.6f)
            {
                _movement.Stop();
                Dwell(_watchTvUntil - Time.time, 1f); // TV 앞에 앉아 본다 — 감각은 그대로, 방향만 TV에 고정
                return;
            }
            if (_movement.Arrived || _movement.Velocity.sqrMagnitude < 0.01f) _movement.MoveTo(_tvWatchPoint, _balance.CatPatrolSpeed);
        }

        private void FaceTv()
        {
            Vector3 d = _tvPos - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>TV를 보는 중 (테스트·디버그).</summary>
        public bool IsWatchingTv => _watchingTv && _dwelling;
        private bool AvoidingVacuum => Time.time < _avoidVacuumUntil;

        private int FindSpot(CatSpotType type)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_spots[i].Type != type) continue;
                float d = Vector3.Distance(transform.position, _spots[i].Pos);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private Vector3 RandomDoor(Vector3 fallback)
        {
            int count = 0;
            foreach (var s in _spots) if (s.Type == CatSpotType.Door) count++;
            if (count == 0) return fallback;
            int pick = Random.Range(0, count);
            foreach (var s in _spots) if (s.Type == CatSpotType.Door && pick-- == 0) return s.Pos;
            return fallback;
        }

        // Update에서 (호스트): 놀이·포획이 끝나면 미뤄 둔 부르기
        private void TickHousePending()
        {
            if (!_callAwayPending) return;
            if (State.Value == CatState.Toy || State.Value == CatState.Capture) return;
            _callAwayPending = false;
            SetState(CatState.Away);
        }

        // SetState(Away)에서 호출
        private void EnterAwayState()
        {
            TargetClientId.Value = 0;
            _chaseTarget = null;
            _feedingUntil = 0f;
            _senses.ConsumeStimulus();
            _awayUntil = -1f;
            _awayWalkDeadline = Time.time + 15f;
            int door = FindSpot(CatSpotType.Door);
            Vector3 exit = door >= 0 ? _spots[door].Pos : FarthestSpotFromPlayers();
            _movement.MoveTo(exit, _balance.CatAwayWalkSpeed);
            Log.Dev($"고양이 [{name}]: 부르는 소리 — 문으로 ({exit:F0}, {_awaySeconds:0}s 부재)");
        }

        // SetState가 Away 밖으로 나갈 때
        private void ExitAway()
        {
            if (AwayHidden.Value) AwayHidden.Value = false;
        }

        private void TickAway()
        {
            if (!AwayHidden.Value)
            {
                if (!_movement.Arrived && Time.time < _awayWalkDeadline) return;
                // 문 밖으로 — 안 보이고 아무것도 못 느낀다
                _movement.Stop();
                AwayHidden.Value = true;
                _awayUntil = Time.time + _awaySeconds;
                Log.Dev($"고양이 [{name}]: 나감");
                return;
            }
            _senses.SensitivityMultiplier = 0f;
            _senses.ConsumeStimulus();
            if (Time.time < _awayUntil) return;
            // 어느 문으로 돌아올지 모른다
            Vector3 entry = RandomDoor(transform.position);
            GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(entry);
            Log.Dev($"고양이 [{name}]: 돌아옴 ({entry:F0})");
            SetState(CatState.Return); // ExitAway가 다시 보이게
        }

        private Vector3 FarthestSpotFromPlayers()
        {
            Vector3 best = transform.position; float bestD = -1f;
            foreach (var s in _spots)
            {
                float nearest = float.MaxValue;
                foreach (var c in NetworkManager.ConnectedClientsList)
                    if (c.PlayerObject != null) nearest = Mathf.Min(nearest, Vector3.Distance(c.PlayerObject.transform.position, s.Pos));
                if (nearest > bestD) { bestD = nearest; best = s.Pos; }
            }
            return best;
        }
    }
}
