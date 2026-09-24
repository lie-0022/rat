using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 귀 돌리기 (design/cat-ideas/13, 2026-09-24 고양이 42 — 필러 2 읽히는 고양이). 귀가 관심 쪽을 향한다.
    /// 들을 뻔한 작은 소리에도 귀만 쫑긋 — 게이지가 오르기 전의 텔레그래프. 상태가 대상을 가지면(추격·조사·유인·호기심) 계속 그쪽.
    /// 호스트가 EarPoint·EarActive를 정하고, 귀 회전은 CatVisual(모든 클라).
    /// </summary>
    public partial class CatBrain
    {
        public NetworkVariable<Vector3> EarPoint = new NetworkVariable<Vector3>(Vector3.zero);
        public NetworkVariable<bool> EarActive = new NetworkVariable<bool>(false);

        private float _earUntil;
        private float _nextEarSync;

        // OnNetworkSpawn(호스트)에서
        private void InitEars() => _senses.EarHeard += OnEarHeard;

        private void OnEarHeard(Vector3 pos)
        {
            _earUntil = Time.time + _balance.CatEarHoldSeconds;
            SetEar(pos, force: true);
        }

        // Update(호스트)에서
        private void TickEars()
        {
            if (Time.time < _nextEarSync) return;
            _nextEarSync = Time.time + 0.25f;
            if (TryStateEarTarget(out var p)) { SetEar(p, force: false); return; }
            if (Time.time >= _earUntil && EarActive.Value) EarActive.Value = false;
        }

        private bool TryStateEarTarget(out Vector3 p)
        {
            p = default;
            switch (State.Value)
            {
                case CatState.Chase:
                    if (_chaseTarget == null) return false;
                    p = _chaseTarget.transform.position; return true;
                case CatState.Suspicious:
                case CatState.Search:
                    p = _investigatePos; return true;
                case CatState.Distracted:
                    p = _distractPos; return true;
                case CatState.Curious:
                    if (_curiousItem == null) return false;
                    p = _curiousItem.transform.position; return true;
                default:
                    return false;
            }
        }

        private void SetEar(Vector3 p, bool force)
        {
            if (force || !EarActive.Value || (EarPoint.Value - p).sqrMagnitude > 0.09f) EarPoint.Value = p;
            if (!EarActive.Value) EarActive.Value = true;
        }
    }
}
