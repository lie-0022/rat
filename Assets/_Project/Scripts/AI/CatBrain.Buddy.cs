using RatGame.Core;
using RatGame.Player;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 짝꿍 관계에서 고양이가 하는 일 (design/cat-ideas/07, 2026-09-24). 지시는 CatRelation(중재자)이 한다.
    ///  협공(Flank): 짝꿍이 쫓는 쥐의 2초 뒤 예상 위치로 돌아 들어간다. "그럴듯하게만" — 보이면 평소대로 추격.
    ///  공동 수면: 짝꿍이 잠든 잠자리로 가서 같이, 더 오래 잔다(×1.5).
    /// </summary>
    public partial class CatBrain
    {
        private PlayerCondition _flankTarget;
        private float _flankUntil;
        private float _nextFlankStep;
        private Vector3 _flankPrevPos;
        private bool _buddySleepBonus;   // 다음 잠 1회 ×1.5

        public bool IsKitten => Personality != null && Personality.IsKitten;
        private Vector3 _respondTo;

        private int PickRandomPersonality()
        {
            var pool = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _personalities.Length; i++) if (_personalities[i] != null && !_personalities[i].RoleOnly) pool.Add(i);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : 0;
        }

        // 아기 포획: 쥐를 넘어뜨리기만 (기존 Stunned 2s) — 잡지는 못한다
        private void KittenKnockdown(PlayerCondition victim)
        {
            victim.ServerSetState(ConditionState.Stunned);
            Log.Dev($"고양이 [{name}]: 아기가 client {victim.OwnerClientId} 넘어뜨림 (기절)");
            SetState(CatState.Return);
        }

        /// <summary>호스트: 아기가 불렀다 — 추격 속도로 달려가 그 자리 조사.</summary>
        public void ServerRespond(Vector3 pos)
        {
            if (!IsServer) return;
            var st = State.Value;
            if (st is CatState.Chase or CatState.Capture or CatState.Toy or CatState.Away or CatState.Fight) return;
            _respondTo = pos;
            SetState(CatState.Respond);
            _movement.MoveTo(CatMovement.Sample(pos, 2f, pos), _balance.CatChaseSpeed);
            Log.Dev($"고양이 [{name}]: 아기가 부른다 — 달려감 ({pos:F0})");
        }

        private void TickRespond()
        {
            if (CheckEscalation()) return; // 가는 길에 쥐가 보이면 평소대로
            if (!_movement.Arrived) return;
            _investigatePos = _respondTo;
            _senses.RaiseGaugeTo(_balance.CatSuspicionThreshold, _respondTo);
            SetState(CatState.Suspicious);
        }

        public bool CanAssistBuddy => State.Value is CatState.Patrol or CatState.Return or CatState.Suspicious
                                      or CatState.Curious or CatState.Track or CatState.Search;

        /// <summary>호스트: 짝꿍이 쫓는 쥐를 앞질러 막으러 간다.</summary>
        public void ServerFlank(PlayerCondition target)
        {
            if (!IsServer || target == null || !CanAssistBuddy) return;
            _flankTarget = target;
            _flankUntil = Time.time + _balance.CatFlankSeconds;
            _nextFlankStep = 0f;
            _flankPrevPos = target.transform.position;
            SetState(CatState.Flank);
            Log.Dev($"고양이 [{name}]: 협공 — client {target.OwnerClientId} 앞질러");
        }

        /// <summary>호스트: 짝꿍이 잠든 잠자리로 가서 같이 잔다.</summary>
        public void ServerJoinSleep()
        {
            if (!IsServer || !(State.Value is CatState.Patrol or CatState.Return)) return;
            int bed = FindSpot(CatSpotType.Bed);
            if (bed < 0) return;
            _buddySleepBonus = true;
            SetState(CatState.Patrol);
            CancelMemoryVisit();
            _dwelling = false;
            _spotIndex = bed;
            _movement.MoveTo(_spots[bed].Pos, _balance.CatPatrolSpeed);
            Log.Dev($"고양이 [{name}]: 짝꿍 옆에서 자러 ({_spots[bed].Name})");
        }

        // SetState(Sleep)에서: 짝꿍과 같이 자면 더 오래
        private float BuddySleepMul()
        {
            if (!_buddySleepBonus) return 1f;
            _buddySleepBonus = false;
            return _balance.CatBuddySleepMul;
        }

        private void TickFlank()
        {
            if (CheckEscalation()) return; // 보이면 평소대로 추격
            var t = _flankTarget;
            if (t == null || !t.IsSpawned || t.State.Value != ConditionState.Active || Time.time >= _flankUntil)
            {
                SetState(CatState.Return);
                return;
            }
            if (Time.time < _nextFlankStep) return;
            float dt = Time.time - (_nextFlankStep - 0.5f);
            _nextFlankStep = Time.time + 0.5f;
            Vector3 pos = t.transform.position;
            Vector3 vel = (pos - _flankPrevPos) / Mathf.Max(0.1f, dt); vel.y = 0f;
            _flankPrevPos = pos;
            // 예상 도주 지점 — 너무 정확하면 도망이 불가능하니 최대 속도로 자르고 NavMesh에 샘플
            Vector3 lead = pos + Vector3.ClampMagnitude(vel, _balance.CatChaseSpeed) * _balance.CatFlankLeadSeconds;
            _movement.MoveTo(CatMovement.Sample(lead, 2f, pos), _balance.CatFlankSpeed);
        }
    }
}
