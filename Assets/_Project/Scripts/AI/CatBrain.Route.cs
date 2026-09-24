using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 순찰꾼 (docs/07, 고양이 92). 큰길 순찰 지점을 순서대로 오가고 끝에서 되돌아온다 — 큰길을 위험하게 해 우회 선택을 살린다.
    /// 유인·소리·추격은 평소대로라 끝나면 순찰로 돌아와 가까운 지점부터 다시 오간다. 호스트 AI만(동기화 없음).
    /// </summary>
    public partial class CatBrain
    {
        private bool _isPatroller;
        private readonly List<int> _route = new();
        private int _routePos = -1;
        private int _routeDir = 1;

        public bool IsPatroller => _isPatroller;

        /// <summary>호스트: 순찰 지점(CatSpot이 이미 놓인 자리) 순서대로 오가게 한다.</summary>
        public void ServerMakePatroller(List<Vector3> points)
        {
            if (!IsServer || points == null || points.Count < 2) return;
            _isPatroller = true;
            if (_personalities != null)
                for (int i = 0; i < _personalities.Length; i++)
                    if (_personalities[i] != null && _personalities[i].IsPatroller) { ServerSetPersonality(i); break; }
            CollectSpots();
            _route.Clear();
            foreach (var p in points)
                for (int i = 0; i < _spots.Length; i++)
                    if ((_spots[i].Pos - p).sqrMagnitude < 0.09f) { _route.Add(i); break; }
            _routePos = -1; _routeDir = 1; _spotIndex = -1;
            _recentSpots.Clear();
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && _route.Count > 0) agent.Warp(_spots[_route[0]].Pos); // 큰길에서 시작 — 먼 방에서 자다 오면 초반엔 큰길이 비어서
            Log.Dev($"고양이 [{name}]: 순찰꾼 — 큰길 지점 {_route.Count}개");
        }

        // GoToNextSpot 앞에서: 순찰꾼이면 다음 지점(끝에서 되돌아옴). 처음이나 딴 데 다녀온 뒤엔 가까운 지점부터
        private bool TryRouteNext()
        {
            if (!_isPatroller || _route.Count < 2) return false;
            if (_routePos < 0 || _spotIndex != _route[_routePos]) _routePos = NearestRoutePos();
            else
            {
                _routePos += _routeDir;
                if (_routePos >= _route.Count || _routePos < 0) { _routeDir = -_routeDir; _routePos += 2 * _routeDir; }
            }
            _spotIndex = _route[_routePos];
            _movement.MoveTo(_spots[_spotIndex].Pos, _balance.CatPatrolSpeed);
            return true;
        }

        private int NearestRoutePos()
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < _route.Count; i++)
            {
                float d = (_spots[_route[i]].Pos - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
