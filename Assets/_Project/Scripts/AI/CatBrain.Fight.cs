using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 앙숙 싸움 (design/cat-ideas/07, 2026-09-24). 시작 판정은 CatRelation(호스트 중재자)이 한다 — 각 고양이는 상대만 안다.
    /// 둘이 가운데 지점 주변을 빙글빙글 돌며 15s. 그동안 소리를 못 듣고(Deaf) 시야는 ×0.2 — 팀이 복도를 뚫을 틈.
    /// 단 2m 안 쥐 목격(CloseSight)은 못 참는다 → 싸움을 깨고 추격.
    /// </summary>
    public partial class CatBrain
    {
        private CatBrain _fightWith;
        private float _fightUntil;
        private float _nextFightStep;

        public CatBrain FightOpponent => State.Value == CatState.Fight ? _fightWith : null;
        /// <summary>지금 쫓는 쥐 (짝꿍 협공 — CatRelation이 읽는다).</summary>
        public RatGame.Player.PlayerCondition ChaseTarget => State.Value == CatState.Chase ? _chaseTarget : null;

        /// <summary>싸울 수 있는 상태인가 (CatRelation이 묻는다).</summary>
        public bool CanFight => State.Value is CatState.Patrol or CatState.Return or CatState.Suspicious
                                or CatState.Curious or CatState.Track or CatState.Search;

        /// <summary>호스트: 중재자가 싸움 시작.</summary>
        public void ServerStartFight(CatBrain other, float seconds)
        {
            if (!IsServer || other == null) return;
            _fightWith = other;
            _fightUntil = Time.time + seconds;
            SetState(CatState.Fight);
        }

        private void EnterFightState()
        {
            _nextFightStep = 0f;
            _senses.Deaf = true;
            _senses.ConsumeStimulus();
            TargetClientId.Value = 0;
            Log.Dev($"고양이 [{name}]: 하악! {(_fightWith != null ? _fightWith.name : "?")}와 싸움");
        }

        private void ExitFight()
        {
            _senses.Deaf = false;
            _fightWith = null;
        }

        private void TickFight()
        {
            _senses.SensitivityMultiplier = _balance.CatFightViewMul; // 서로에게 꽂혀 거의 못 본다
            // 코앞 쥐는 못 참는다
            if (_senses.VisibleTarget != null && _senses.CloseSight)
            {
                Log.Dev($"고양이 [{name}]: 싸우다 코앞 쥐 발견 — 추격");
                _chaseTarget = _senses.VisibleTarget;
                SetState(CatState.Chase);
                return;
            }
            // 상대가 먼저 빠졌으면(쥐를 쫓으러 등) 혼자 싸우지 않는다
            if (_fightWith == null || !_fightWith.IsSpawned || _fightWith.State.Value != CatState.Fight || Time.time >= _fightUntil)
            {
                Log.Dev($"고양이 [{name}]: 싸움 끝");
                SetState(CatState.Return);
                return;
            }
            Vector3 mid = (transform.position + _fightWith.transform.position) * 0.5f;
            Vector3 face = _fightWith.transform.position - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            if (Time.time < _nextFightStep) return;
            _nextFightStep = Time.time + 1f;
            _movement.MoveTo(_movement.RandomPointAround(mid, 1.2f), 3f);
        }
    }
}
