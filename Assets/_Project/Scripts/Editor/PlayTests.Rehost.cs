using System.Collections.Generic;
using RatGame.Net;
using RatGame.Run;
using RatGame.UI;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 세션 나갔다 다시 호스트 (고양이 298) — 메뉴 → 호스트 → 기지 → "세션 나가기" → 메뉴를 두 번 되풀이한 뒤 창고로 출발까지.
    /// 정적 상태·이벤트 구독이 남아 두 번째 세션에서 깨지는 흔한 버그를 잡는다. 오류·예외 0이어야.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Leave And Rehost Twice")]
        private static void ArmRehost() => Arm("rehost");

        private static int _rehostErrors;
        private static void CountRehostErrors(string msg, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) { _rehostErrors++; if (_rehostErrors <= 3) Report.Append($" | 오류: {msg.Split('\n')[0]}"); }
        }

        private static Step HostFromMenu(string name) => new Step { Name = name, Wait = 1f, Ready = () => Object.FindFirstObjectByType<MainMenuController>() != null && SceneManager.GetActiveScene().name == "MainMenu", Act = () =>
        {
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
        } };

        private static Step InHub(string name) => new Step { Name = name, Wait = 1f, Ready = () =>
            SceneManager.GetActiveScene().name == "Hub" && NetworkManager.Singleton.LocalClient?.PlayerObject != null || EditorApplication.timeSinceStartup - _stepAt > 20,
            Check = () => SceneManager.GetActiveScene().name == "Hub" && NetworkManager.Singleton.LocalClient?.PlayerObject != null ? null : "기지·내 쥐가 안 섬" };

        private static Step Leave(string name) => new Step { Name = name, Wait = 1.5f, Act = () => Object.FindFirstObjectByType<NetworkLauncher>().Shutdown() };

        private static List<Step> RehostSteps() => new()
        {
            new Step { Name = "오류 세기", Act = () =>
            {
                _rehostErrors = 0;
                Application.logMessageReceived -= CountRehostErrors;
                Application.logMessageReceived += CountRehostErrors;
                OnDone = () => Application.logMessageReceived -= CountRehostErrors;
            } },
            HostFromMenu("호스트 1"), InHub("기지 1"), Leave("나가기 1"),
            HostFromMenu("호스트 2"), InHub("기지 2"), Leave("나가기 2"),
            HostFromMenu("호스트 3"), InHub("기지 3"),
            new Step { Name = "창고 출발", Ready = () =>
            {
                if (SceneManager.GetActiveScene().name == "Stage_Warehouse01" && RunManager.Instance != null && RunManager.Instance.Phase.Value == RunPhase.StageActive) return true;
                if (EditorApplication.timeSinceStartup - _stepAt > 40) return true;
                var pad = Object.FindFirstObjectByType<DeparturePad>();
                if (pad != null && pad.IsSpawned && SceneManager.GetActiveScene().name == "Hub") Put(pad.transform.position + Vector3.up * 0.5f);
                return false;
            }, Check = () => RunManager.Instance != null && RunManager.Instance.Phase.Value == RunPhase.StageActive ? null : "세 번째 세션에서 출발이 안 됨" },
            new Step { Name = "오류", Wait = 2f, Check = () => { Report.Append($" | 오류·예외 {_rehostErrors}"); return _rehostErrors == 0 ? null : $"오류 {_rehostErrors}"; } },
        };
    }
}
