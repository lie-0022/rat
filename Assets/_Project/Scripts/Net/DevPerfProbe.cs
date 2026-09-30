using System.Collections.Generic;
using RatGame.Core;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 개발 빌드 프레임 시간 측정 (고양이 86, docs/13 3-3 "그레이박스 144fps"). 10초 창마다 평균·1% 최악·최대를 계산해
    /// DEV 패널이 보여 주고, `-perflog` 인자면 로그로도 남긴다(빌드 소크에서 읽으려고 — 에디터 수치는 참고가 안 됨).
    /// `-perflog`는 수직 동기화를 꺼서 모니터 주사율에 묶이지 않은 값을 잰다. 씬·프리팹을 안 건드리게 스스로 생긴다.
    /// </summary>
    public class DevPerfProbe : MonoBehaviour
    {
        private const float WindowSeconds = 10f;

        public static float AvgMs { get; private set; }
        public static float Worst1Ms { get; private set; }
        public static bool HasData { get; private set; }

        private readonly List<float> _samples = new(2048);
        private float _windowStart;
        private bool _log;
        // GC 할당 — 스크립트 시간(0.23ms)은 작아도 할당이 쌓이면 GC 멈춤이 1% 최악을 만든다 (고양이 324)
        private ProfilerRecorder _gcAlloc;
        private long _gcBytes;
        private int _gcFrames, _gcCountAtStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("DevPerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<DevPerfProbe>();
        }

        private void Awake()
        {
            _log = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perflog") >= 0;
            if (_log) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 1000; }
            _windowStart = Time.unscaledTime;
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcCountAtStart = System.GC.CollectionCount(0);
        }

        private void OnDestroy() => _gcAlloc.Dispose();

        private void Update()
        {
            if (_gcAlloc.Valid) { _gcBytes += _gcAlloc.LastValue; _gcFrames++; }
            float dt = Time.unscaledDeltaTime;
            if (dt <= 1f) _samples.Add(dt * 1000f); // 1초 넘는 프레임은 로딩·에디터 멈춤 — 게임 프레임이 아님 (DEV 패널에 "FPS 0"이 뜨던 것)
            if (_samples.Count == 0) { _windowStart = Time.unscaledTime; return; }
            if (Time.unscaledTime - _windowStart < WindowSeconds) return;
            _windowStart = Time.unscaledTime;

            float sum = 0f, max = 0f;
            foreach (var s in _samples) { sum += s; if (s > max) max = s; }
            AvgMs = sum / _samples.Count;
            _samples.Sort();
            int worstCount = Mathf.Max(1, _samples.Count / 100);
            float worst = 0f;
            for (int i = _samples.Count - worstCount; i < _samples.Count; i++) worst += _samples[i];
            Worst1Ms = worst / worstCount;
            HasData = true;
            if (_log)
            {
                var nm = NetworkManager.Singleton;
                int netObjs = nm != null && nm.SpawnManager != null ? nm.SpawnManager.SpawnedObjectsList.Count : 0;
                int agents = FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None).Length; // 고양이 수 (NavMeshAgent는 고양이뿐)
                Log.Dev($"성능: 평균 {AvgMs:F2}ms ({1000f / AvgMs:F0}fps), 1% 최악 {Worst1Ms:F2}ms, 최대 {max:F1}ms, 프레임 {_samples.Count}, " +
                        $"씬 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, 네트 오브젝트 {netObjs}, 고양이 {agents}, " +
                        $"GC 할당 {(_gcFrames > 0 ? _gcBytes / 1024f / _gcFrames : 0f):F1}KB/프레임, GC {System.GC.CollectionCount(0) - _gcCountAtStart}번");
            }
            _gcBytes = 0; _gcFrames = 0; _gcCountAtStart = System.GC.CollectionCount(0);
            _samples.Clear();
        }
    }
}
