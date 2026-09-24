using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 문지기 (docs/07, 고양이 80). 목적지 앞 초소 둘레 스팟만 순찰하고 초소를 자주 들른다.
    /// 유인·소리·추격은 평소대로라 끌려갔다가, 순찰로 돌아오면 다시 초소 둘레로 — 상점 유인 물건을 쓸 이유.
    /// 호스트 AI만 바뀐다(동기화 없음).
    /// </summary>
    public partial class CatBrain
    {
        private bool _isGuard;
        private Vector3 _guardPost;

        public bool IsGuard => _isGuard;

        /// <summary>호스트: 초소(CatSpot이 이미 놓인 자리)를 지키게 한다. 스팟을 다시 모아 초소를 포함시킨다.</summary>
        public void ServerMakeGuard(Vector3 post)
        {
            if (!IsServer) return;
            _isGuard = true;
            _guardPost = post;
            // 문지기 성격(러시안 블루)으로 — 성격은 이미 동기화되니 클라도 색·이름으로 알아본다
            if (_personalities != null)
                for (int i = 0; i < _personalities.Length; i++)
                    if (_personalities[i] != null && _personalities[i].IsGuard) { ServerSetPersonality(i); break; }
            CollectSpots();
            _recentSpots.Clear();
            _spotIndex = -1;
            // 처음부터 초소에 — 먼 방에서 자다 깨면 스테이지 초반엔 문이 비어서. 초소에서 잠들면 몰래 지나갈 기회
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && UnityEngine.AI.NavMesh.SamplePosition(post, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) agent.Warp(hit.position);
            Log.Dev($"고양이 [{name}]: 문지기 — 초소 {post:F1}, 둘레 스팟 {GuardEligibleCount()}개");
        }

        // 문지기면 초소 둘레 밖 스팟은 0, 초소 자체는 가중
        private float GuardFilter(int i, float w)
        {
            if (!_isGuard || w <= 0f) return w;
            Vector3 d = _spots[i].Pos - _guardPost; d.y = 0f;
            float r = _balance.CatGuardRadius;
            if (d.sqrMagnitude > r * r) return 0f;
            return d.sqrMagnitude < 0.25f ? w * _balance.CatGuardPostWeight : w;
        }

        private int GuardEligibleCount()
        {
            int n = 0;
            for (int i = 0; i < _spots.Length; i++) if (SpotWeight(i) > 0f) n++;
            return n;
        }
    }
}
