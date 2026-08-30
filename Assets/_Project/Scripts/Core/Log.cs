using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace RatGame.Core
{
    /// <summary>개발용 로그 래퍼. 릴리즈 빌드에서 Dev() 호출은 통째로 스트립된다 (CLAUDE.md 컨벤션).</summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Dev(string message) => Debug.Log($"[Rat] {message}");

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DevWarn(string message) => Debug.LogWarning($"[Rat] {message}");

        // 에러는 릴리즈에서도 남긴다 — 진행 불가 버그 추적용
        public static void Error(string message) => Debug.LogError($"[Rat] {message}");
    }
}
