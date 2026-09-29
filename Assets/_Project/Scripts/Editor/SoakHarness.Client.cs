using System.IO;
using System.Text.RegularExpressions;
using RatGame.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 통째 시험 2인 — macOS 빌드 클라를 붙인다 (고양이 201). 클라는 -autojoin(자동 접속)·-autoshop(스테이지마다 자동 구매),
    /// 로그는 따로 받아 끝나면 경고·오류·예외를 센다. 빌드는 미리 Builds/macOS/Rat.app에 (Build 메뉴·MCP).
    /// </summary>
    public static partial class SoakHarness
    {
        private const string ClientCountKey = "RatGame.Soak.ClientCount";
        private const string ClientApp = "Builds/macOS/Rat.app";
        private static int _clientCount; // 빌드 클라 수 — 0 혼자, 1 = 2인, 3 = 4인 (고양이 209)
        private static bool _twoPlayer => _clientCount > 0;
        private static bool _clientLaunched, _clientSettled;
        private static float _clientWaitStart, _clientMoveAt, _clientJoinedAt;
        private static string ClientLog(int i) => Path.GetFullPath(i == 0 ? "Temp/soak-client.log" : $"Temp/soak-client{i + 1}.log");

        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls, 2P build client)")]
        private static void ArmTwoPlayer()
        {
            SessionState.SetInt(ClientCountKey, 1);
            Arm(false);
        }

        // 4인 — 빌드 클라 3개 (docs/03·09 4인 수용 기준의 자동판, 고양이 209)
        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls, 4P build clients)")]
        private static void ArmFourPlayer()
        {
            SessionState.SetInt(ClientCountKey, 3);
            Arm(false);
        }

        // 2인 + 영어 — 호스트가 묶어 보낸 문장(상점 결과 등)을 클라가 영어로 푸는지 (고양이 202). 클라는 같은 settings.json을 읽어 영어로 뜬다
        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls, 2P build client, English)")]
        private static void ArmTwoPlayerEnglish()
        {
            SessionState.SetInt(ClientCountKey, 1);
            Arm(true);
        }

        private static int _clientActions, _pingsSeen, _squeaksSeen;

        // 스테이지마다 모든 클라가 킁킁·핑·찍찍 (예전 소크 3~13처럼) — 호스트에서 핑(EventBus.PingReceived)·찍찍(RatSqueak)이 도착하는지 센다
        private static void ClientActions()
        {
            if (!_twoPlayer) return;
            var nm = NetworkManager.Singleton;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                var rc = nm.ConnectedClients[id].PlayerObject.GetComponent<RatGame.Net.DevRemoteControl>();
                rc.ServerSend("sniff"); rc.ServerSend("ping"); rc.ServerSend("squeak");
                _clientActions++;
            }
        }

        private static void OnPingSeen(ulong owner, Vector3 _) { if (owner != NetworkManager.Singleton.LocalClientId) _pingsSeen++; }
        private static void OnSqueakSeen(ulong owner, Vector3 _) { if (owner != NetworkManager.Singleton.LocalClientId) _squeaksSeen++; }

        private static void BeginClient()
        {
            _clientActions = _pingsSeen = _squeaksSeen = 0;
            RatGame.Core.EventBus.PingReceived -= OnPingSeen; RatGame.Core.EventBus.PingReceived += OnPingSeen;
            RatGame.Core.EventBus.RatSqueak -= OnSqueakSeen; RatGame.Core.EventBus.RatSqueak += OnSqueakSeen;
            _clientCount = SessionState.GetInt(ClientCountKey, 0);
            SessionState.SetInt(ClientCountKey, 0);
            _clientLaunched = false; _clientSettled = false; _clientJoinedAt = 0f; _clientMoveAt = 0f;
        }

        /// <summary>기지에서 호스트가 열린 뒤 클라를 띄우고 접속을 기다린다. 준비되면 true.</summary>
        private static bool ClientReady()
        {
            if (!_twoPlayer) return true;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return false;
            if (!_clientLaunched)
            {
                if (!Directory.Exists(ClientApp)) { Finish($"빌드 없음 ({ClientApp})"); return false; }
                // 빌드 클라는 호스트 play가 뜬 뒤에 띄운다 (먼저 띄우면 재시도 끝에 실패 — loop-state 테스트 요령). 로그는 클라마다 따로(같은 Player.log를 덮으므로)
                for (int i = 0; i < _clientCount; i++)
                {
                    if (File.Exists(ClientLog(i))) File.Delete(ClientLog(i));
                    System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport -autojoin -autoshop -logFile \"{ClientLog(i)}\"");
                }
                _clientLaunched = true;
                _clientWaitStart = Time.realtimeSinceStartup;
                Debug.Log($"[Rat] 통째 시험: 빌드 클라 {_clientCount}개 띄움");
            }
            if (nm.ConnectedClientsIds.Count >= 1 + _clientCount)
            {
                // 붙자마자 보낸 순간이동은 클라 쪽 스폰 전이라 사라진다 — 3초 뒤 기지 단계 시계를 새로 (클라 앱 켜는 데 수십 초라 45초 제한을 다 쓴다)
                if (_clientJoinedAt == 0f) _clientJoinedAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - _clientJoinedAt < 3f) return false;
                if (!_clientSettled) { _clientSettled = true; Go(Step.Hub); }
                return true;
            }
            if (Time.realtimeSinceStartup - _clientWaitStart > 90f) Finish($"클라가 90초 안에 다 안 붙음 ({nm.ConnectedClientsIds.Count - 1}/{_clientCount})");
            return false;
        }

        /// <summary>호스트 말고 붙은 쥐들을 옮긴다 — 클라 이동은 소유 클라 권한이라 RPC로 (docs/03).</summary>
        private static void MoveClients(Vector3 pos)
        {
            if (!_twoPlayer || Time.realtimeSinceStartup < _clientMoveAt) return;
            _clientMoveAt = Time.realtimeSinceStartup + 1f; // 매 프레임 RPC 도배 안 하게
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            int k = 0;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                var player = nm.ConnectedClients[id].PlayerObject;
                var controller = player != null ? player.GetComponent<PlayerController>() : null;
                if (controller == null) continue;
                k++;
                var rpc = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } };
                // 서로 겹치면 튕겨 기절한다 (고양이 133 — 시작 자리 겹침). 발판이 2.5m라 한 줄로 세우면 넷째가 밖 — 호스트 둘레 2×2 격자 0.6m
                var offset = k switch { 1 => new Vector3(0.6f, 0f, 0f), 2 => new Vector3(0f, 0f, 0.6f), _ => new Vector3(0.6f, 0f, 0.6f) };
                controller.TeleportClientRpc(pos + offset, Quaternion.identity, rpc);
            }
        }

        // 클라를 닫고 로그에서 경고·오류·예외를 센다 — 개발 빌드는 경고·오류 뒤에 "UnityEngine.Debug:LogWarning" 같은 줄이 붙는다
        private static string CloseClientAndReport()
        {
            RatGame.Core.EventBus.PingReceived -= OnPingSeen;
            RatGame.Core.EventBus.RatSqueak -= OnSqueakSeen;
            if (!_twoPlayer) return "";
            System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS");
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < _clientCount; c++) sb.Append(ReportClientLog(c));
            return sb.ToString();
        }

        // 개발 빌드 로그는 메시지 뒤에 스택이 줄줄이 온다 — 위로 올라가 스택 줄("(at "·"UnityEngine."로 시작)이 아닌 첫 줄 = 메시지 (고양이 224)
        private static string MessageAbove(string[] lines, int i)
        {
            for (int k = i - 1; k >= 0 && k >= i - 12; k--)
            {
                string l = lines[k];
                if (l.Length == 0 || l.StartsWith("UnityEngine.") || l.StartsWith("RatGame.") || l.StartsWith("Unity.") || l.StartsWith("System.")) continue;
                return l;
            }
            return lines[i];
        }

        private static string ReportClientLog(int c)
        {
            string path = ClientLog(c), tag = _clientCount == 1 ? "클라" : $"클라{c + 1}";
            if (!File.Exists(path)) return $" | {tag} 로그 없음";
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (IOException) { return $" | {tag} 로그 못 읽음"; }
            int warn = 0, err = 0, ex = 0, rat = 0;
            string firstIssue = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.StartsWith("[Rat]")) rat++;
                bool w = l.Contains("Debug:LogWarning"), e = l.Contains("Debug:LogError"), x = Regex.IsMatch(l, @"^\w*Exception");
                if (w) warn++; if (e) err++; if (x) ex++;
                if ((w || e || x) && firstIssue == null) firstIssue = x ? l : MessageAbove(lines, i);
            }
            return $" | {tag}: [Rat] 줄 {rat} · 경고 {warn} · 오류 {err} · 예외 {ex}{(firstIssue != null ? $" · 첫 문제: {firstIssue}" : "")}";
        }
    }
}
