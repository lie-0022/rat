using System.Collections.Generic;
using RatGame.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.AI
{
    /// <summary>
    /// 고양이 길 막힘 감시 (개발 빌드 `-perflog`만, 고양이 340) — 갈 길이 2m 넘게 남았는데(걷는 상태만) 6초 동안 거의 안 움직이면 한 번 로그.
    /// 스팟에서 머무르는 건 남은 길이 없어 안 잡힌다. 긴 세션(성능 측정)에서 NavMesh·문·통로가 고양이를 가두는지 본다. 호스트만(고양이 두뇌는 호스트).
    /// </summary>
    public class DevCatStuckWatch : MonoBehaviour
    {
        private const float StuckSeconds = 6f;
        private const float MinRemaining = 2f; // 멈춰 서는 상태(잠·상자·부름)는 정지 거리만큼 1.6m쯤 남는다 — 그보다 길게
        private const float MoveEpsilon = 0.15f;

        /// <summary>이번 실행에서 잡힌 막힘 횟수 — 성능 줄이 같이 적는다.</summary>
        public static int StuckCount { get; private set; }

        private readonly Dictionary<CatBrain, (Vector3 pos, float since, bool reported)> _seen = new();
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => StuckCount = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perflog") < 0) return;
            var go = new GameObject("DevCatStuckWatch");
            DontDestroyOnLoad(go);
            go.AddComponent<DevCatStuckWatch>();
        }

        // 일부러 서 있는 상태(잠·상자 앉기·매복·부름·놀기·싸움…)는 빼고 걸어서 어딘가 가려는 상태만
        private static bool Moving(CatState s) => s is CatState.Patrol or CatState.Return or CatState.Suspicious or CatState.Chase
            or CatState.Track or CatState.Search or CatState.Flank or CatState.Respond or CatState.Zoomies;

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 1f;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) { _seen.Clear(); return; }
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                var agent = cat.GetComponent<NavMeshAgent>();
                Vector3 pos = cat.transform.position;
                bool wantsToMove = Moving(cat.State.Value) && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped
                                   && agent.hasPath && !agent.pathPending && agent.remainingDistance > MinRemaining;
                if (!_seen.TryGetValue(cat, out var s) || !wantsToMove || (pos - s.pos).sqrMagnitude > MoveEpsilon * MoveEpsilon)
                {
                    if (s.reported) Log.DevWarn($"고양이 길 막힘 풀림: {cat.name} {Time.time - s.since:0}초 만에 ({cat.State.Value})");
                    _seen[cat] = (pos, Time.time, false);
                    continue;
                }
                if (!s.reported && Time.time - s.since >= StuckSeconds)
                {
                    StuckCount++;
                    _seen[cat] = (s.pos, s.since, true);
                    Log.DevWarn($"고양이 길 막힘: {cat.name} {cat.State.Value} @ {pos:F1} → {agent.destination:F1} (남은 {agent.remainingDistance:F1}m, 길 {agent.pathStatus}, 회피 우선 {agent.avoidancePriority}, {StuckSeconds:0}초 제자리)");
                }
            }
        }
    }
}
