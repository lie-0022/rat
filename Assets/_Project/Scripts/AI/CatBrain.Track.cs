using System.Collections.Generic;
using RatGame.Core;
using RatGame.Noise;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 냄새 추적 (design/cat-ideas/06, 2026-09-24). 순찰·복귀 중 반경 3m 안에서 쥐가 남긴 자국을 맡으면
    /// 그 자국 → 같은 쥐의 다음 자국 순으로 따라간다(자국마다 킁킁 0.5s). 목격·자극이 오면 끊긴다.
    /// 자국이 끊기면 끝점 3m 안 숨을 곳을 수색하고, 없으면 복귀. 한 번 따라간 자국은 다시 안 쫓는다.
    /// </summary>
    public partial class CatBrain
    {
        private ScentMark _trackMark;
        private float _trackSniffUntil = -1f;
        private int _trackFollowed;
        private readonly Dictionary<ulong, int> _trackedUpTo = new();

        /// <summary>이번 추적에서 맡은 자국 수 (테스트·로그용).</summary>
        public int TrackMarksFollowed => _trackFollowed;

        private float _nextBedCoverCheck;

        // Update에서 (호스트, 0.25s마다): 잠자리에 올라간 쥐는 고양이 냄새로 덮인다
        private void TickBedCover()
        {
            if (Time.time < _nextBedCoverCheck || _spots == null) return;
            _nextBedCoverCheck = Time.time + 0.25f;
            float r2 = _balance.BedCoverRadius * _balance.BedCoverRadius;
            foreach (var s in _spots)
            {
                if (s.Type != CatSpotType.Bed) continue;
                foreach (var client in NetworkManager.ConnectedClientsList)
                {
                    var po = client.PlayerObject; if (po == null) continue;
                    Vector3 d = po.transform.position - s.Pos; d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    var scent = po.GetComponent<RatGame.Player.PlayerScent>();
                    if (scent != null && !scent.IsCovered) Log.Dev($"냄새 덮기: client {client.ClientId} 고양이 침대");
                    if (scent != null) scent.ServerCoverScent(_balance.BedCoverSeconds);
                }
            }
        }

        private bool TrackFilter(ulong source, int seq) => !_trackedUpTo.TryGetValue(source, out int upTo) || seq > upTo;

        private bool CheckScent()
        {
            if (!ScentSystem.FindNearest(transform.position, _balance.CatScentDetectRadius, _balance.CatScentMinStrength, TrackFilter, out var mark))
                return false;
            _trackMark = mark;
            SetState(CatState.Track);
            return true;
        }

        // SetState(Track)에서 호출
        private void EnterTrackState()
        {
            _trackFollowed = 0;
            _trackSniffUntil = -1f;
            _movement.MoveTo(CatMovement.Sample(_trackMark.Pos, 1.5f, _trackMark.Pos), _balance.CatTrackSpeed);
            Log.Dev($"고양이 [{name}]: 냄새 추적 — client {_trackMark.Source} 자국 #{_trackMark.Seq} (강도 {ScentSystem.StrengthOf(_trackMark):0})");
        }

        private void TickTrack()
        {
            if (CheckEscalation()) return;

            if (_trackSniffUntil < 0f)
            {
                if (!_movement.Arrived)
                {
                    if (_movement.Velocity.sqrMagnitude < 0.01f)
                        _movement.MoveTo(CatMovement.Sample(_trackMark.Pos, 1.5f, _trackMark.Pos), _balance.CatTrackSpeed);
                    return;
                }
                _movement.Stop();
                _trackSniffUntil = Time.time + _balance.CatTrackSniffSeconds;
                _trackFollowed++;
                _trackedUpTo[_trackMark.Source] = Mathf.Max(_trackMark.Seq, _trackedUpTo.TryGetValue(_trackMark.Source, out int u) ? u : 0);
                return;
            }
            if (Time.time < _trackSniffUntil) return;

            if (ScentSystem.NextInTrail(_trackMark.Source, _trackMark.Seq, _balance.CatScentMinStrength, out var next))
            {
                _trackMark = next;
                _trackSniffUntil = -1f;
                _movement.MoveTo(CatMovement.Sample(next.Pos, 1.5f, next.Pos), _balance.CatTrackSpeed);
                return;
            }

            // 자국이 끊겼다 — 끝점 바로 옆에 숨을 곳이 있으면 거기 숨었다고 본다
            Vector3 end = _trackMark.Pos;
            Log.Dev($"고양이 [{name}]: 냄새 끊김 — 자국 {_trackFollowed}개 따라옴, 끝 {end:F1}");
            if (TryEnterSearch(end, 3f)) return; // 자국 간격 1.5m + 입구까지 — 3m 안 숨을 곳
            SetState(CatState.Return);
        }
    }
}
