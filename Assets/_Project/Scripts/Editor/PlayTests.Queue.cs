using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [MenuItem("Tools/RatGame/Test/Run All — Quick (solo 9)")]
        private static void RunQuick() => StartQueue("carry,chase,achv,codexall,attention,modifiers,rehost,langswitch,panels");

        [MenuItem("Tools/RatGame/Test/Run All — Full (solo + 2P + 4P + leave)")]
        private static void RunFull() => StartQueue("carry,chase,achv,codexall,attention,modifiers,rehost,langswitch,panels,rescue2p,glue2p,chase2p,sync4p,laser2p,rejoin2p,leave2p,hostleave2p");

        // 전부 — 짧은 시험 11 + 통째 시험 혼자·영어 2인·4인 (고양이 287, 약 15분). 빌드 클라가 필요하다
        [MenuItem("Tools/RatGame/Test/Run Everything (Full + soaks)")]
        private static void RunEverything() => StartQueue("carry,chase,achv,codexall,attention,modifiers,rehost,langswitch,panels,rescue2p,glue2p,chase2p,sync4p,laser2p,rejoin2p,leave2p,hostleave2p,soak1p,soaken2p,soak4p");

        private static void StartQueue(string keys)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Rat] 시험 묶음은 플레이가 꺼진 상태에서 시작"); return; }
            if (!EnsureBootScene()) return;
            SessionState.SetString(QueueKey, keys);
            SessionState.SetString(ResultsKey, "");
            Debug.Log($"[Rat] 시험 묶음 시작: {keys}");
            ContinueQueue();
        }

        // 시험은 늘 Boot 씬에서 켠다 — 빌드 뒤 빈 씬(Untitled)이 열려 있으면 AutoBoot가 시험 포트를 고르기 전에 자동 호스트해
        // (7777은 새어 있고) 17개가 모두 3분 초과로 실패했다 (고양이 327). 저장 안 한 변경이 있으면 건드리지 않고 멈춘다
        internal static bool EnsureBootScene()
        {
            const string boot = "Assets/_Project/Scenes/Boot.unity";
            var active = SceneManager.GetActiveScene();
            if (active.path == boot) return true;
            string name = string.IsNullOrEmpty(active.path) ? "이름 없는 씬" : active.path;
            if (active.isDirty) { Debug.LogError($"[Rat] 시험 안 함 — {name}에 저장 안 한 변경이 있음. 저장하거나 버린 뒤 다시"); return false; }
            EditorSceneManager.OpenScene(boot, OpenSceneMode.Single);
            Debug.Log($"[Rat] 시험 씬: {name} → Boot");
            return true;
        }

        // 편집 모드로 돌아올 때마다 — 남은 게 있으면 다음 시험을 건다
        private static void ContinueQueue()
        {
            string queue = SessionState.GetString(QueueKey, "");
            if (string.IsNullOrEmpty(queue)) return;
            int comma = queue.IndexOf(',');
            string next = comma < 0 ? queue : queue.Substring(0, comma);
            SessionState.SetString(QueueKey, comma < 0 ? "" : queue.Substring(comma + 1));
            if (next.StartsWith("soak")) { SoakHarness.ArmFromQueue(next); return; } // 통째 시험은 제 무장·시작 — 끝나면 QueueResult로 돌아온다
            SessionState.SetString(ArmedKey, next);
            // 바로 켠다 — 에디터가 뒤에 있으면 편집 모드에선 delayCall·update가 몇 분씩 안 불려 묶음이 멈췄다(고양이 239·248·249)
            EditorApplication.isPlaying = true;
        }

        // 시험 하나가 끝나면 결과를 적고 플레이를 멈춘다(멈추면 ContinueQueue). 묶음이 아니면 아무것도 안 함
        internal static void QueueResult(string name, string mark)
        {
            string results = SessionState.GetString(ResultsKey, null);
            if (results == null) return; // 묶음 밖에서 돌린 시험
            results += (results.Length > 0 ? "|" : "") + $"{mark} {name}";
            SessionState.SetString(ResultsKey, results);
            if (string.IsNullOrEmpty(SessionState.GetString(QueueKey, "")))
            {
                var parts = results.Split('|');
                int ok = 0, skipped = 0;
                foreach (var p in parts) { if (p.StartsWith("✓")) ok++; else if (p.StartsWith("–")) skipped++; }
                Debug.Log($"[Rat] 시험 묶음 끝 — {parts.Length}개 중 {ok} 통과{(skipped > 0 ? $" · {skipped} 건너뜀" : "")} | {results}");
                SessionState.EraseString(ResultsKey);
            }
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }
    }
}
