using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 빌드 메뉴 (docs/13 3-6, 고양이 320) — 데모 목표는 Windows x64 릴리스. 개발 빌드는 Builds/{플랫폼}(시험 도구가 쓰는 자리),
    /// 릴리스는 Builds/Release/{플랫폼}에 따로 — 릴리스로 시험 클라를 덮으면 DevRemoteControl이 없어 2인 시험이 깨진다.
    /// 배치 모드: <c>Unity -batchmode -quit -projectPath . -executeMethod RatGame.EditorTools.BuildTools.BuildWindowsDemoCli</c>
    /// </summary>
    public static class BuildTools
    {
        private const string BootScene = "Assets/_Project/Scenes/Boot.unity";
        private const string BurstSymbols = "_BurstDebugInformation_DoNotShip";

        [MenuItem("Tools/RatGame/Build/Windows x64 Demo (release)")]
        private static void WindowsRelease() => Build(BuildTarget.StandaloneWindows64, false);

        [MenuItem("Tools/RatGame/Build/Windows x64 (dev)")]
        private static void WindowsDev() => Build(BuildTarget.StandaloneWindows64, true);

        [MenuItem("Tools/RatGame/Build/macOS (release)")]
        private static void MacRelease() => Build(BuildTarget.StandaloneOSX, false);

        [MenuItem("Tools/RatGame/Build/macOS (dev)")]
        private static void MacDev() => Build(BuildTarget.StandaloneOSX, true);

        /// <summary>배치 모드용 — 실패하면 종료 코드 1 (CI·스크립트가 알아채게).</summary>
        public static void BuildWindowsDemoCli()
        {
            string error = Build(BuildTarget.StandaloneWindows64, false);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }

        public static string OutputPath(BuildTarget target, bool dev)
        {
            string platform = target == BuildTarget.StandaloneOSX ? "macOS" : "Windows";
            string file = target == BuildTarget.StandaloneOSX ? "Rat.app" : "Rat.exe";
            return dev ? $"Builds/{platform}/{file}" : $"Builds/Release/{platform}/{file}";
        }

        /// <summary>빌드 전 검사 — 문제가 있으면 사람이 읽을 이유, 없으면 null.</summary>
        public static string Preflight(BuildTarget target, out string[] scenes)
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (EditorApplication.isPlaying) return "플레이 중 — 멈추고 다시";
            if (EditorApplication.isCompiling) return "컴파일 중 — 끝나고 다시";
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                return $"{target} 빌드 모듈이 없음 — Unity Hub → 설치 → 톱니 → 모듈 추가 → " +
                       (target == BuildTarget.StandaloneOSX ? "Mac Build Support" : "Windows Build Support (Mono)") + ", 에디터 재시작";
            if (scenes.Length == 0 || scenes[0] != BootScene) return $"빌드 씬 0번이 Boot가 아님 ({(scenes.Length > 0 ? scenes[0] : "비어 있음")}) — docs/01";
            foreach (var s in scenes) if (!File.Exists(s)) return $"빌드 씬 파일이 없음: {s}";
            return null;
        }

        public static string Build(BuildTarget target, bool dev)
        {
            string error = Preflight(target, out var scenes);
            string path = OutputPath(target, dev);
            string kind = $"{(target == BuildTarget.StandaloneOSX ? "macOS" : "Windows x64")} {(dev ? "개발" : "릴리스")} v{PlayerSettings.bundleVersion}";
            if (error != null)
            {
                Debug.LogError($"[Rat] 빌드 안 함 — {kind}: {error}");
                return error;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = dev ? BuildOptions.Development : BuildOptions.None,
            });
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                error = $"{summary.result}, 오류 {summary.totalErrors}";
                Debug.LogError($"[Rat] 빌드 실패 — {kind}: {error}");
                return error;
            }

            // 릴리스엔 Burst 디버그 기호를 싣지 않는다(이름대로) — 지우지 않고 Builds/Symbols로 옮겨 크래시 분석용으로 둔다
            string moved = dev ? null : MoveBurstSymbols(path, target);
            Debug.Log($"[Rat] 빌드 성공 — {kind} → {path} · {summary.totalSize / (1024f * 1024f):0}MB · {summary.totalTime.TotalSeconds:0}초 · 경고 {summary.totalWarnings}" +
                      (moved != null ? $" · 기호 → {moved}" : ""));
            return null;
        }

        private static string MoveBurstSymbols(string path, BuildTarget target)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path) + BurstSymbols;
            string from = Path.Combine(dir, name);
            if (!Directory.Exists(from)) return null;
            string to = $"Builds/Symbols/{(target == BuildTarget.StandaloneOSX ? "macOS" : "Windows")}-{PlayerSettings.bundleVersion}";
            if (Directory.Exists(to)) Directory.Delete(to, true);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            Directory.Move(from, to);
            return to;
        }
    }
}
