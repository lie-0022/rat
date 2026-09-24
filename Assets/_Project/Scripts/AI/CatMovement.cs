using UnityEngine;
using UnityEngine.AI;

namespace RatGame.AI
{
    /// <summary>NavMeshAgent 래퍼 (docs/07). 호스트에서만 활성 — 클라는 CatBrain이 disable 처리.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CatMovement : MonoBehaviour
    {
        private NavMeshAgent _agent;

        public bool Arrived => !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
        /// <summary>몸이 향한 방향과 다음 경로점 방향의 일치도 (-1~1). 코너에서 낮다 — 추격 감속용.</summary>
        public float SteeringAlignment
        {
            get
            {
                if (!_agent.enabled || !_agent.isOnNavMesh || !_agent.hasPath) return 1f;
                Vector3 to = _agent.steeringTarget - transform.position; to.y = 0f;
                return to.sqrMagnitude < 0.01f ? 1f : Vector3.Dot(transform.forward, to.normalized);
            }
        }

        /// <summary>NavMesh 위 가장 가까운 지점 (반경 안에 없으면 fallback).</summary>
        public static Vector3 Sample(Vector3 pos, float radius, Vector3 fallback) =>
            NavMesh.SamplePosition(pos, out var hit, radius, NavMesh.AllAreas) ? hit.position : fallback;
        public Vector3 Velocity => _agent.velocity;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoTraverseOffMeshLink = false; // 선반 점프는 직접 — 호를 그리고 가끔 실패한다 (고양이 39)
        }

        // ---- 점프 (NavMeshLink) ----
        /// <summary>올라가는 점프를 실패할지 (CatBrain이 준다). null이면 실패 없음.</summary>
        public System.Func<bool> JumpFailCheck { get; set; }
        /// <summary>점프 실패 — 원래 자리로 떨어졌다.</summary>
        public event System.Action JumpFailed;
        public float JumpSeconds { get; set; } = 0.45f;
        public bool IsJumping => _jumping;

        private bool _jumping;
        private bool _jumpFail;
        private float _jumpT;
        private Vector3 _jumpFrom, _jumpTo;

        private void Update()
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            if (!_jumping)
            {
                if (!_agent.isOnOffMeshLink) return;
                var link = _agent.currentOffMeshLinkData;
                _jumpFrom = transform.position;
                _jumpTo = link.endPos + Vector3.up * _agent.baseOffset;
                _jumpT = 0f;
                _jumping = true;
                bool up = _jumpTo.y > _jumpFrom.y + 0.3f;
                _jumpFail = up && JumpFailCheck != null && JumpFailCheck();
                return;
            }
            _jumpT += Time.deltaTime / Mathf.Max(0.05f, JumpSeconds);
            float t = Mathf.Clamp01(_jumpT);
            if (_jumpFail && t >= 0.5f)
            {
                // 머리를 박고 떨어진다 — 링크를 버리고 출발점으로
                _jumping = false;
                _agent.Warp(_jumpFrom);
                JumpFailed?.Invoke();
                return;
            }
            float arc = Mathf.Sin(t * Mathf.PI) * 0.8f;
            transform.position = Vector3.Lerp(_jumpFrom, _jumpTo, t) + Vector3.up * arc;
            if (t < 1f) return;
            _jumping = false;
            _agent.CompleteOffMeshLink();
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            _agent.enabled = value;
        }

        /// <summary>모든 이동 속도 배율 (성격 — 아기 0.75). CatBrain이 성격 적용 때 설정.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public void MoveTo(Vector3 pos, float speed)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.speed = speed * SpeedMultiplier;
            _agent.isStopped = false;
            _agent.SetDestination(World.RoomDoor.Redirect(transform.position, pos)); // 닫힌 문 너머면 문 앞으로 (고양이 40)
        }

        public void Stop()
        {
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        /// <summary>
        /// 경로 없이 직접 밀기 (미끄러짐 — design/cat-ideas/11). NavMesh 밖으로는 안 나가고 경계·장애물 모서리에서 멈춘다.
        /// 실제로 움직인 수평 거리를 돌려준다 — 요청보다 훨씬 작으면 벽에 박은 것.
        /// </summary>
        public float Shove(Vector3 delta)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return 0f;
            if (_agent.hasPath) _agent.ResetPath();
            Vector3 before = _agent.nextPosition;
            _agent.Move(delta);
            Vector3 moved = _agent.nextPosition - before; moved.y = 0f;
            return moved.magnitude;
        }

        /// <summary>중심 주변 반경 내 랜덤 NavMesh 지점 (Suspicious 배회용).</summary>
        public Vector3 RandomPointAround(Vector3 center, float radius)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 candidate = center + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
                if (NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas))
                    return hit.position;
            }
            return center;
        }
    }
}
