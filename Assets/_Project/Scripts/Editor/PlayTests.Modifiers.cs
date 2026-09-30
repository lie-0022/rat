using System;
using System.Collections.Generic;
using RatGame.Core;
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
    /// "오늘의 집" 7종을 하나씩 (고양이 296) — 벽 속 스테이지를 조건마다 다시 열어(F4 "오늘의 집"과 같은 길) 맵이 서고 오류·예외가 0인지.
    /// 통째 시험은 무작위로 한두 조건만 지나가서 정전·손님 같은 드문 조건이 오래 안 돌 수 있다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Stage Modifiers All 7")]
        private static void ArmModifiers() => Arm("modifiers");

        private static int _modErrors;
        private static void CountModErrors(string msg, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) { _modErrors++; if (_modErrors <= 3) Report.Append($" | 오류: {msg.Split('\n')[0]}"); }
        }

        private static bool StageUp() =>
            SceneManager.GetActiveScene().name == "Stage_Walls" && RunManager.Instance != null && RunManager.Instance.IsSpawned
            && RunManager.Instance.Phase.Value == RunPhase.StageActive && Object.FindFirstObjectByType<GridZoneBuilder>() != null;

        private static List<Step> ModifierSteps()
        {
            var steps = new List<Step>
            {
                new Step { Name = "호스트", Wait = 1f, Ready = () => Object.FindFirstObjectByType<MainMenuController>() != null, Act = () =>
                {
                    _modErrors = 0;
                    Application.logMessageReceived -= CountModErrors;
                    Application.logMessageReceived += CountModErrors;
                    OnDone = () => Application.logMessageReceived -= CountModErrors;
                    var menu = Object.FindFirstObjectByType<MainMenuController>();
                    var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
                } },
                // 기지 발판 목적지를 벽 속으로 → 발판 위에서 출발
                new Step { Name = "벽 속 출발", Ready = () =>
                {
                    if (StageUp()) return true;
                    if (EditorApplication.timeSinceStartup - _stepAt > 60) return true;
                    var pad = Object.FindFirstObjectByType<DeparturePad>();
                    if (pad != null && pad.IsSpawned && SceneManager.GetActiveScene().name == "Hub")
                    {
                        for (int i = 0; i < 4 && !pad.DestinationName.Contains("벽 속"); i++) pad.ServerCycleDestination();
                        Put(pad.transform.position + Vector3.up * 0.5f);
                    }
                    return false;
                }, Check = () => StageUp() ? null : "벽 속 스테이지가 안 열림" },
            };
            foreach (StageModifier m in Enum.GetValues(typeof(StageModifier)))
            {
                var mod = m;
                steps.Add(new Step { Name = $"{mod} 열기", Wait = 1f, Act = () =>
                {
                    GridZoneBuilder.DevForceModifier = mod;
                    RunSession.DepartPending = true;
                    if (GameStateMachine.Instance.Current == GameState.InRun) GameStateMachine.Instance.TransitionTo(GameState.Lobby); // F4와 같게 (고양이 114)
                    NetworkManager.Singleton.SceneManager.LoadScene("Stage_Walls", LoadSceneMode.Single);
                } });
                steps.Add(new Step { Name = $"{mod}", Wait = 2f, Ready = () =>
                    (StageUp() && Object.FindFirstObjectByType<GridZoneBuilder>().Modifier == mod && EditorApplication.timeSinceStartup - _stepAt > 4) || EditorApplication.timeSinceStartup - _stepAt > 30,
                    Check = () =>
                    {
                        var b = Object.FindFirstObjectByType<GridZoneBuilder>();
                        if (b == null || !StageUp()) return "스테이지가 안 섬";
                        return b.Modifier == mod ? null : $"조건이 {b.Modifier}";
                    } });
            }
            steps.Add(new Step { Name = "오류", Check = () => { Report.Append($" | 오류·예외 {_modErrors}"); return _modErrors == 0 ? null : $"오류 {_modErrors}"; } });
            return steps;
        }
    }
}
