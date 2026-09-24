using RatGame.Core;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 환경을 쓰는 고양이 — 매복·상자 (design/cat-ideas/09, 2026-09-24). "어디서 나올지 모른다."
    ///  매복: Ambush 스팟에서 60s 숨음. 몸은 안 보이고 꼬리만(텔레그래프 타협 불가) + 10s마다 "츄릅" 자막. 2m 안 쥐 → 덮치기(스윙 0.2s).
    ///  상자: Box 스팟(쥐의 숨을 곳 입구)에 앉아 15~40s. 시야 정면 3m·45°만. 앉은 동안 그 숨을 곳은 드나들 수 없다(갇힘).
    ///       도착했을 때 안에 쥐가 있으면 끄집어내 덮친다 — 누가 먼저 차지하느냐.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>매복 중 — 꼬리만 보인다 (CatVisual).</summary>
        public NetworkVariable<bool> AmbushHidden = new NetworkVariable<bool>(false);

        private float _lurkUntil;
        private float _nextLurkCue;
        private bool _pounceSwing;       // 다음 Capture 스윙을 짧게 (덮치기)
        private HideSpot _boxHideSpot;

        // ArriveAtSpot에서 — 매복 스팟 도착
        private void EnterAmbush() => SetState(CatState.Ambush);

        // ArriveAtSpot에서 — 상자 스팟 도착
        private void EnterBoxSit() => SetState(CatState.BoxSit);

        private void EnterAmbushState()
        {
            _movement.Stop();
            _lurkUntil = Time.time + _balance.CatAmbushSeconds;
            _nextLurkCue = Time.time + _balance.CatAmbushCueInterval;
            AmbushHidden.Value = true;
            Log.Dev($"고양이 [{name}]: 매복 ({_balance.CatAmbushSeconds}s)");
        }

        private void EnterBoxSitState()
        {
            _movement.Stop();
            var r = _balance.CatBoxSeconds;
            _lurkUntil = Time.time + Random.Range(r.x, r.y);
            _boxHideSpot = NearestHideSpot(_balance.CatBoxBlockRadius);
            if (_boxHideSpot != null)
            {
                var ejected = _boxHideSpot.ServerEject();
                if (ejected.Count > 0)
                {
                    // 먼저 들어가 있던 쥐 — 끄집어내 바로 덮친다
                    Log.Dev($"고양이 [{name}]: 상자에 쥐가! {ejected.Count}명 끄집어냄");
                    _chaseTarget = ejected[0];
                    _pounceSwing = true;
                    SetState(CatState.Capture);
                    return;
                }
                _boxHideSpot.CatBlocking.Value = true;
            }
            if (_spotIndex >= 0) transform.rotation = Quaternion.LookRotation(SpotFacing());
            _senses.ViewDistanceOverride = _balance.CatBoxViewDistance;
            _senses.ViewHalfAngleOverride = _balance.CatBoxViewHalfAngle;
            Log.Dev($"고양이 [{name}]: 상자 입구에 앉음 ({_lurkUntil - Time.time:0}s{(_boxHideSpot != null ? $", {_boxHideSpot.DisplayName} 막음" : "")})");
        }

        // SetState가 매복·상자 밖으로 나갈 때
        private void ExitLurk()
        {
            if (AmbushHidden.Value) AmbushHidden.Value = false;
            if (_boxHideSpot != null && _boxHideSpot.CatBlocking.Value) _boxHideSpot.CatBlocking.Value = false;
            _boxHideSpot = null;
            _senses.ViewDistanceOverride = null;
            _senses.ViewHalfAngleOverride = null;
        }

        private void TickLurk()
        {
            // 가까운 쥐는 추격 없이 바로 덮친다
            var seen = _senses.VisibleTarget;
            if (seen != null && Vector3.Distance(seen.transform.position, transform.position) <= _balance.CatAmbushPounceRange)
            {
                Log.Dev($"고양이 [{name}]: 덮치기! client {seen.OwnerClientId}");
                _chaseTarget = seen;
                _pounceSwing = true;
                SetState(CatState.Capture);
                return;
            }
            if (CheckEscalation()) return; // 멀리서 보이면 평소대로
            if (State.Value == CatState.Ambush && Time.time >= _nextLurkCue)
            {
                _nextLurkCue = Time.time + _balance.CatAmbushCueInterval;
                CatCueClientRpc((byte)CatCueKind.Ambush, transform.position);
            }
            if (Time.time >= _lurkUntil) SetState(CatState.Return);
        }

        // SetState(Capture) 직후: 덮치기면 스윙을 짧게
        private void ApplyPounceSwing()
        {
            if (!_pounceSwing) return;
            _pounceSwing = false;
            _captureSwingEnd = Time.time + _balance.CatPounceSwingSeconds;
        }

        private HideSpot NearestHideSpot(float radius)
        {
            if (_hideSpots == null) return null;
            HideSpot best = null; float bestD = radius;
            foreach (var h in _hideSpots)
            {
                if (h == null || !h.IsSpawned) continue;
                float d = Vector3.Distance(h.ExitPosition, transform.position);
                if (d <= bestD) { bestD = d; best = h; }
            }
            return best;
        }

        private Vector3 SpotFacing()
        {
            // 상자 입구 스팟은 바깥을 본다 — 숨을 곳이 있으면 그 반대 방향
            if (_boxHideSpot != null)
            {
                Vector3 away = transform.position - _boxHideSpot.transform.position; away.y = 0f;
                if (away.sqrMagnitude > 0.01f) return away.normalized;
            }
            return transform.forward;
        }
    }
}
