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
        public Vector3 Velocity => _agent.velocity;

        private void Awake() => _agent = GetComponent<NavMeshAgent>();

        public void SetEnabled(bool value)
        {
            enabled = value;
            _agent.enabled = value;
        }

        public void MoveTo(Vector3 pos, float speed)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.speed = speed;
            _agent.isStopped = false;
            _agent.SetDestination(pos);
        }

        public void Stop()
        {
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
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
