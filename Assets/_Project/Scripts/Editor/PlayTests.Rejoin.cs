using System;
using System.Collections.Generic;
using System.IO;
using RatGame.Net;
using RatGame.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 클라가 나갔다 다시 들어옴 (고양이 299) — 기지에서 빌드 클라가 "세션 나가기"(leave) → 호스트가 곧바로 알아챔 → 새 클라가 다시 접속 →
    /// 쥐가 다시 서고 두 쥐가 겹치지 않음, 호스트 오류 0. (프로세스가 죽는 경우는 이탈 시험 264)
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Client Leaves And Rejoins 2P (build client)")]
        private static void ArmRejoin() => Arm("rejoin2p");

        private static double _leftAt;

        private static void LaunchClient(string log)
        {
            if (!Directory.Exists(ClientApp)) throw new Exception($"빌드 없음 ({ClientApp})");
            System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport{DevPort.ClientArg} -autojoin -logFile \"{Path.GetFullPath($"Temp/{log}")}\"");
        }

        private static List<Step> RejoinSteps() => new()
        {
            new Step { Name = "호스트", Wait = 1f, Ready = () => Object.FindFirstObjectByType<MainMenuController>() != null, Act = () =>
            {
                _rehostErrors = 0;
                Application.logMessageReceived -= CountRehostErrors;
                Application.logMessageReceived += CountRehostErrors;
                OnDone = () => { Application.logMessageReceived -= CountRehostErrors; System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS"); };
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
            } },
            new Step { Name = "클라 1", Wait = 2f, Ready = () => SceneManager.GetActiveScene().name == "Hub", Act = () => { LaunchClient("rejoin-client1.log"); _joinedAt = 0; } },
            new Step { Name = "클라 1 접속", Ready = () =>
            {
                if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return EditorApplication.timeSinceStartup - _stepAt > 60;
                if (_joinedAt == 0) _joinedAt = EditorApplication.timeSinceStartup;
                return EditorApplication.timeSinceStartup - _joinedAt > 3;
            }, Check = () => NetworkManager.Singleton.ConnectedClientsIds.Count == 2 ? null : "클라 1이 안 들어옴", Act = () =>
            {
                foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                    if (id != NetworkManager.Singleton.LocalClientId) _client = NetworkManager.Singleton.ConnectedClients[id].PlayerObject;
            } },
            new Step { Name = "세션 나가기", Act = () => { _client.GetComponent<DevRemoteControl>().ServerSend("leave"); _leftAt = EditorApplication.timeSinceStartup; } },
            new Step { Name = "호스트가 앎", Ready = () => NetworkManager.Singleton.ConnectedClientsIds.Count == 1 || EditorApplication.timeSinceStartup - _stepAt > 15, Check = () =>
            {
                Report.Append($" | 나감을 {EditorApplication.timeSinceStartup - _leftAt:0.0}초에 앎");
                return NetworkManager.Singleton.ConnectedClientsIds.Count == 1 ? null : "나간 클라가 남아 있음";
            } },
            new Step { Name = "클라 2", Wait = 1f, Act = () => { System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS"); } },
            new Step { Name = "클라 2 띄움", Wait = 2f, Act = () => { LaunchClient("rejoin-client2.log"); _joinedAt = 0; } },
            new Step { Name = "다시 접속", Ready = () =>
            {
                if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return EditorApplication.timeSinceStartup - _stepAt > 60;
                if (_joinedAt == 0) _joinedAt = EditorApplication.timeSinceStartup;
                return EditorApplication.timeSinceStartup - _joinedAt > 3;
            }, Check = () =>
            {
                var nm = NetworkManager.Singleton;
                if (nm.ConnectedClientsIds.Count != 2) return "다시 안 들어옴";
                NetworkObject other = null;
                foreach (var id in nm.ConnectedClientsIds) if (id != nm.LocalClientId) other = nm.ConnectedClients[id].PlayerObject;
                if (other == null) return "다시 들어온 쥐가 없음";
                float gap = Vector3.Distance(other.transform.position, nm.LocalClient.PlayerObject.transform.position);
                Report.Append($" | 다시 들어온 쥐 client {other.OwnerClientId} · 호스트와 {gap:0.0}m");
                return gap > 0.5f ? null : "두 쥐가 겹침";
            } },
            new Step { Name = "오류", Check = () => { Report.Append($" | 호스트 오류·예외 {_rehostErrors}"); return _rehostErrors == 0 ? null : $"오류 {_rehostErrors}"; } },
        };
    }
}
