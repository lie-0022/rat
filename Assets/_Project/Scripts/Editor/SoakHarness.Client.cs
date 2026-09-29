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
        private const string TwoPlayerKey = "RatGame.Soak.TwoPlayer";
        private const string ClientApp = "Builds/macOS/Rat.app";
        private static bool _twoPlayer, _clientLaunched, _clientSettled;
        private static float _clientWaitStart, _clientMoveAt, _clientJoinedAt;
        private static string ClientLog => Path.GetFullPath("Temp/soak-client.log");

        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls, 2P build client)")]
        private static void ArmTwoPlayer()
        {
            SessionState.SetBool(TwoPlayerKey, true);
            Arm(false);
        }

        // 2인 + 영어 — 호스트가 묶어 보낸 문장(상점 결과 등)을 클라가 영어로 푸는지 (고양이 202). 클라는 같은 settings.json을 읽어 영어로 뜬다
        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls, 2P build client, English)")]
        private static void ArmTwoPlayerEnglish()
        {
            SessionState.SetBool(TwoPlayerKey, true);
            Arm(true);
        }

        private static void BeginClient()
        {
            _twoPlayer = SessionState.GetBool(TwoPlayerKey, false);
            SessionState.SetBool(TwoPlayerKey, false);
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
                if (File.Exists(ClientLog)) File.Delete(ClientLog);
                // 빌드 클라는 호스트 play가 뜬 뒤에 띄운다 (먼저 띄우면 재시도 끝에 실패 — loop-state 테스트 요령)
                System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport -autojoin -autoshop -logFile \"{ClientLog}\"");
                _clientLaunched = true;
                _clientWaitStart = Time.realtimeSinceStartup;
                Debug.Log("[Rat] 통째 시험: 빌드 클라 띄움");
            }
            if (nm.ConnectedClientsIds.Count >= 2)
            {
                // 붙자마자 보낸 순간이동은 클라 쪽 스폰 전이라 사라진다 — 3초 뒤 기지 단계 시계를 새로 (클라 앱 켜는 데 수십 초라 45초 제한을 다 쓴다)
                if (_clientJoinedAt == 0f) _clientJoinedAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - _clientJoinedAt < 3f) return false;
                if (!_clientSettled) { _clientSettled = true; Go(Step.Hub); }
                return true;
            }
            if (Time.realtimeSinceStartup - _clientWaitStart > 60f) Finish("클라가 60초 안에 안 붙음");
            return false;
        }

        /// <summary>호스트 말고 붙은 쥐들을 옮긴다 — 클라 이동은 소유 클라 권한이라 RPC로 (docs/03).</summary>
        private static void MoveClients(Vector3 pos)
        {
            if (!_twoPlayer || Time.realtimeSinceStartup < _clientMoveAt) return;
            _clientMoveAt = Time.realtimeSinceStartup + 1f; // 매 프레임 RPC 도배 안 하게
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                var player = nm.ConnectedClients[id].PlayerObject;
                var controller = player != null ? player.GetComponent<PlayerController>() : null;
                if (controller == null) continue;
                var rpc = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } };
                controller.TeleportClientRpc(pos + Vector3.right * 0.6f, Quaternion.identity, rpc);
            }
        }

        // 클라를 닫고 로그에서 경고·오류·예외를 센다 — 개발 빌드는 경고·오류 뒤에 "UnityEngine.Debug:LogWarning" 같은 줄이 붙는다
        private static string CloseClientAndReport()
        {
            if (!_twoPlayer) return "";
            System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS");
            if (!File.Exists(ClientLog)) return " | 클라 로그 없음";
            string[] lines;
            try { lines = File.ReadAllLines(ClientLog); }
            catch (IOException) { return " | 클라 로그 못 읽음"; }
            int warn = 0, err = 0, ex = 0, rat = 0;
            string firstIssue = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.StartsWith("[Rat]")) rat++;
                bool w = l.Contains("Debug:LogWarning"), e = l.Contains("Debug:LogError"), x = Regex.IsMatch(l, @"^\w*Exception");
                if (w) warn++; if (e) err++; if (x) ex++;
                if ((w || e || x) && firstIssue == null) firstIssue = x ? l : (i >= 1 ? lines[i - 1] : l);
            }
            return $" | 클라: [Rat] 줄 {rat} · 경고 {warn} · 오류 {err} · 예외 {ex}{(firstIssue != null ? $" · 첫 문제: {firstIssue}" : "")}";
        }
    }
}
