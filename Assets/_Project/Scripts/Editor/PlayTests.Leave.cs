using System.Collections.Generic;
using System.IO;
using RatGame.Net;
using RatGame.Player;
using RatGame.Run;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 스테이지 도중 나가기 (고양이 264, docs/03 "접속 해제 처리" — docs/13 3-3 진행 불가 버그 0).
    /// ① 클라 이탈: 클라가 상자를 든 채 프로세스가 꺼짐 → 호스트에서 쥐가 사라지고 상자가 풀리고, 혼자 귀환해 결과까지 간다(멈추지 않음), 오류 0.
    /// ② 호스트 이탈: 호스트가 세션을 끔 → 클라가 "호스트와 연결이 끊겨" 메인 메뉴로, 클라 예외 0.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Client Leaves Mid-Stage 2P (build client)")]
        private static void ArmLeave() => Arm("leave2p");

        [MenuItem("Tools/RatGame/Test/Host Leaves Mid-Stage 2P (build client)")]
        private static void ArmHostLeave() => Arm("hostleave2p");

        private static NetworkObject _leaveBox, _leaveBox2;
        private static int _leaveErrors;

        private static void CountErrors(string msg, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) _leaveErrors++;
        }

        private static List<Step> LeaveSteps()
        {
            var steps = new List<Step>(ClientWarehouse("leave-client.log"));
            steps.AddRange(new List<Step>
            {
                new Step { Name = "상자 앞에", Wait = 2f, Act = () =>
                {
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    var at = new Vector3(zone.center.x, zone.min.y + 0.6f, zone.center.z - 6f);
                    TeleportClient(at, 0f);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/GrayBox_S.prefab");
                    var go = Object.Instantiate(prefab, at + new Vector3(0f, 0f, 0.7f), Quaternion.identity);
                    go.GetComponent<NetworkObject>().Spawn(true);
                    _leaveBox = go.GetComponent<NetworkObject>();
                } },
                // 순간이동이 늦게 도착하면 첫 잡기가 허공 — 잡을 때까지 1초마다 다시 (최대 10초)
                new Step { Name = "클라 잡기", Ready = () =>
                {
                    if (_client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == _leaveBox.NetworkObjectId) return true;
                    if (EditorApplication.timeSinceStartup - _stepAt > 10) return true;
                    // 잡기는 누를 때마다 들고/놓기가 바뀐다 — 손이 빈 걸 확인했을 때만 다시 (첫 판: 두 번 눌러 첫 상자를 도로 놓음)
                    if (_client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == 0 && EditorApplication.timeSinceStartup - _lastTp > 1.5) { _lastTp = EditorApplication.timeSinceStartup; _client.GetComponent<DevRemoteControl>().ServerSend("grab"); }
                    return false;
                },
                    Check = () => _client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == _leaveBox.NetworkObjectId ? null : "클라가 상자를 못 잡음" },
                // 2번 칸으로 바꾸면 든 상자는 주머니로 → 두 번째 상자를 새 손에 (이탈 때 손·주머니 둘 다 풀려야)
                new Step { Name = "칸 바꾸기", Act = () => _client.GetComponent<DevRemoteControl>().ServerSend("slot:1") },
                // 단계는 Act 바로 뒤에 Check — 클라가 칸을 바꿀 때까지 기다리는 단계를 따로
                new Step { Name = "주머니로", Ready = () => _leaveBox.GetComponent<CarryableItem>().Pocketed.Value || EditorApplication.timeSinceStartup - _stepAt > 5,
                    Check = () => _leaveBox.GetComponent<CarryableItem>().Pocketed.Value ? null : "첫 상자가 주머니로 안 감" },
                new Step { Name = "두 번째 상자", Act = () =>
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/GrayBox_S.prefab");
                    var go = Object.Instantiate(prefab, _client.transform.position + _client.transform.forward * 0.7f + Vector3.up * 0.2f, Quaternion.identity);
                    go.GetComponent<NetworkObject>().Spawn(true);
                    _leaveBox2 = go.GetComponent<NetworkObject>();
                    _lastTp = 0;
                } },
                new Step { Name = "두 번째 잡기", Ready = () =>
                {
                    if (_client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == _leaveBox2.NetworkObjectId) return true;
                    if (EditorApplication.timeSinceStartup - _stepAt > 10) return true;
                    // 잡기는 누를 때마다 들고/놓기가 바뀐다 — 손이 빈 걸 확인했을 때만 다시 (첫 판: 두 번 눌러 첫 상자를 도로 놓음)
                    if (_client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == 0 && EditorApplication.timeSinceStartup - _lastTp > 1.5) { _lastTp = EditorApplication.timeSinceStartup; _client.GetComponent<DevRemoteControl>().ServerSend("grab"); }
                    return false;
                }, Check = () => _client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == _leaveBox2.NetworkObjectId ? null : "두 번째 상자를 못 잡음" },
                new Step { Name = "클라 꺼짐", Act = () =>
                {
                    _leaveErrors = 0;
                    Application.logMessageReceived -= CountErrors;
                    Application.logMessageReceived += CountErrors;
                    var before = OnDone;
                    OnDone = () => { Application.logMessageReceived -= CountErrors; before?.Invoke(); };
                    System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS");
                } },
                // 프로세스가 죽으면 UnityTransport는 끊김 제한 시간(DisconnectTimeoutMS, 기본 30초) 뒤에야 안다 — 40초까지
                new Step { Name = "이탈 처리", Ready = () => NetworkManager.Singleton.ConnectedClientsIds.Count == 1 || EditorApplication.timeSinceStartup - _stepAt > 40,
                    Check = () =>
                    {
                        var box = _leaveBox2 != null ? _leaveBox2.GetComponent<CarryableItem>() : null;   // 손에 든 것
                        bool gone = _client == null || !_client.IsSpawned;
                        Report.Append($" | 접속 {NetworkManager.Singleton.ConnectedClientsIds.Count} · 클라 쥐 {(gone ? "사라짐" : "남음")} · 상자 캐리어 {(box != null ? box.CarrierIds.Count : -1)}");
                        if (NetworkManager.Singleton.ConnectedClientsIds.Count != 1) return "40초 안에 이탈 처리 안 됨";
                        Report.Append($" · 알아챈 시간 {EditorApplication.timeSinceStartup - _stepAt:0}초");
                        if (!gone) return "나간 클라의 쥐가 남음";
                        var pocket = _leaveBox != null ? _leaveBox.GetComponent<CarryableItem>() : null;   // 주머니에 든 것
                        Report.Append($" · 주머니 상자 {(pocket == null ? "없어짐" : pocket.Pocketed.Value ? "주머니에 남음" : "바닥에")}");
                        if (box == null || box.CarrierIds.Count != 0) return "나간 클라가 든 상자가 안 풀림";
                        return pocket != null && !pocket.Pocketed.Value ? null : "나간 클라의 주머니 상자가 안 나옴";
                    } },
                // 혼자 남은 호스트가 귀환해 결과까지 — 나간 쥐 때문에 "모두 모여야" 조건에 묶이면 안 된다
                new Step { Name = "혼자 귀환", Wait = 1f, Act = () =>
                {
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    Put(zone.center + Vector3.up * 0.2f);
                } },
                new Step { Name = "결과", Ready = () => RunManager.Instance != null && RunManager.Instance.IsShowingResult || EditorApplication.timeSinceStartup - _stepAt > 15,
                    Check = () =>
                    {
                        Report.Append($" | 결과 {(RunManager.Instance != null && RunManager.Instance.IsShowingResult ? "뜸" : "안 뜸")} · 이탈 뒤 오류 {_leaveErrors}");
                        if (RunManager.Instance == null || !RunManager.Instance.IsShowingResult) return "혼자 남은 뒤 귀환이 안 됨";
                        return _leaveErrors == 0 ? null : $"이탈 뒤 오류 {_leaveErrors}";
                    } },
            });
            return steps;
        }

        private static List<Step> HostLeaveSteps()
        {
            string log = Path.GetFullPath("Temp/hostleave-client.log");
            var steps = new List<Step>(ClientWarehouse("hostleave-client.log"));
            steps.AddRange(new List<Step>
            {
                new Step { Name = "호스트 끄기", Wait = 2f, Act = () => Object.FindFirstObjectByType<NetworkLauncher>().Shutdown() }, // 일시정지 창 "세션 나가기"와 같은 길
                new Step { Name = "클라 메뉴로", Ready = () =>
                {
                    if (EditorApplication.timeSinceStartup - _stepAt > 15) return true;
                    return File.Exists(log) && ReadShared(log).Contains("네트워크 종료 — MainMenu 복귀");
                }, Check = () =>
                {
                    string text = File.Exists(log) ? ReadShared(log) : "";
                    int back = text.IndexOf("네트워크 종료 — MainMenu 복귀", System.StringComparison.Ordinal);
                    int exceptions = 0;
                    foreach (var line in text.Split('\n')) if (line.Contains("Exception")) exceptions++;
                    Report.Append($" | 클라 메뉴 복귀 {(back >= 0 ? "함" : "안 함")} · 클라 예외 {exceptions}");
                    if (back < 0) return "호스트가 나갔는데 클라가 메뉴로 안 감";
                    return exceptions == 0 ? null : $"클라 예외 {exceptions}";
                } },
            });
            return steps;
        }

        // 빌드 클라가 쓰는 중인 로그 파일 — 공유 읽기로
        private static string ReadShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }
    }
}
