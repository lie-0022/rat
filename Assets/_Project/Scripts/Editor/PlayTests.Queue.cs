using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 여러 플레이 시험을 차례로 (고양이 238). 시험마다 플레이를 새로 켜고, 끝나면 멈추고, 다음 시험 — 마지막에 한 줄 요약.
    /// 순서·결과는 SessionState(도메인 리로드를 넘김). 2·4인은 빌드(Builds/macOS/Rat.app)가 있어야 한다.
    /// </summary>
    public static partial class PlayTests
    {
        private const string QueueKey = "RatGame.PlayTest.Queue";
        private const string ResultsKey = "RatGame.PlayTest.QueueResults";

        [MenuItem("Tools/RatGame/Test/Run All — Quick (solo 4)")]
        private static void RunQuick() => StartQueue("carry,chase,achv,codexall");

        [MenuItem("Tools/RatGame/Test/Run All — Full (solo + 2P + 4P)")]
        private static void RunFull() => StartQueue("carry,chase,achv,codexall,rescue2p,glue2p,chase2p,sync4p");

        private static void StartQueue(string keys)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Rat] 시험 묶음은 플레이가 꺼진 상태에서 시작"); return; }
            SessionState.SetString(QueueKey, keys);
            SessionState.SetString(ResultsKey, "");
            Debug.Log($"[Rat] 시험 묶음 시작: {keys}");
            ContinueQueue();
        }

        // 편집 모드로 돌아올 때마다 — 남은 게 있으면 다음 시험을 건다
        private static void ContinueQueue()
        {
            string queue = SessionState.GetString(QueueKey, "");
            if (string.IsNullOrEmpty(queue)) return;
            int comma = queue.IndexOf(',');
            string next = comma < 0 ? queue : queue.Substring(0, comma);
            SessionState.SetString(QueueKey, comma < 0 ? "" : queue.Substring(comma + 1));
            SessionState.SetString(ArmedKey, next);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        // 시험 하나가 끝나면 결과를 적고 플레이를 멈춘다(멈추면 ContinueQueue). 묶음이 아니면 아무것도 안 함
        private static void QueueResult(string name, bool passed)
        {
            string results = SessionState.GetString(ResultsKey, null);
            if (results == null) return; // 묶음 밖에서 돌린 시험
            results += (results.Length > 0 ? "|" : "") + $"{(passed ? "✓" : "✗")} {name}";
            SessionState.SetString(ResultsKey, results);
            if (string.IsNullOrEmpty(SessionState.GetString(QueueKey, "")))
            {
                var parts = results.Split('|');
                int ok = 0; foreach (var p in parts) if (p.StartsWith("✓")) ok++;
                Debug.Log($"[Rat] 시험 묶음 끝 — {parts.Length}개 중 {ok} 통과 | {results}");
                SessionState.EraseString(ResultsKey);
            }
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }
    }
}
