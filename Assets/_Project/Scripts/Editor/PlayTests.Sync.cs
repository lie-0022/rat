using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RatGame.Net;
using RatGame.Player;
using RatGame.UI;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 4인 잡기·던지기 동기화 (고양이 236, docs/03 수용 기준 "4인 … 잡기·놓기·던지기 동기화, 위치 어긋남 눈에 띄지 않음"의 수치판).
    /// 기지에서 빌드 클라 3명이 각자 작은 상자(GrayBox_S)를 집어 던짐 → 멈춘 뒤 호스트와 각 클라 화면의 상자 위치를 비교(cm).
    /// 호스트가 한 개를 내려놓기(drop)도 함께. 눈 확인은 사람 항목 그대로.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Throw Sync 4P (3 build clients)")]
        private static void ArmSync() => Arm("sync4p");

        private static readonly List<NetworkObject> SyncBoxes = new();
        private static readonly Dictionary<ulong, Vector3> HostPos = new();

        private static string SyncLog(int i) => Path.GetFullPath($"Temp/sync-c{i + 1}.log");

        private static List<Step> SyncSteps()
        {
            var steps = new List<Step>
            {
                new Step { Name = "호스트", Wait = 1f, Ready = () => UnityEngine.Object.FindFirstObjectByType<MainMenuController>() != null, Act = () =>
                {
                    var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                    var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
                } },
                new Step { Name = "빌드 클라 3", Wait = 2f, Ready = () => SceneManager.GetActiveScene().name == "Hub", Act = () =>
                {
                    if (!Directory.Exists(ClientApp)) throw new Exception($"빌드 없음 ({ClientApp})");
                    for (int i = 0; i < 3; i++)
                    {
                        if (File.Exists(SyncLog(i))) File.Delete(SyncLog(i));
                        System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport{DevPort.ClientArg} -autojoin -logFile \"{SyncLog(i)}\"");
                    }
                    OnDone = () => System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS");
                    _joinedAt = 0;
                } },
                new Step { Name = "클라 접속", Ready = () =>
                {
                    if (NetworkManager.Singleton.ConnectedClientsIds.Count < 4) return false;
                    if (_joinedAt == 0) _joinedAt = EditorApplication.timeSinceStartup;
                    return EditorApplication.timeSinceStartup - _joinedAt > 3;
                }, Act = () =>
                {
                    Clients.Clear();
                    var nm = NetworkManager.Singleton;
                    foreach (var id in nm.ConnectedClientsIds) if (id != nm.LocalClientId) Clients.Add(nm.ConnectedClients[id].PlayerObject);
                } },
                // 발판에서 떨어진 바닥에 상자 넷(클라 3 + 호스트 1), 1.5m 간격 — 각자 자기 상자 앞에 선다
                new Step { Name = "상자 스폰", Wait = 1f, Act = () =>
                {
                    SyncBoxes.Clear();
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/GrayBox_S.prefab");
                    var pad = UnityEngine.Object.FindFirstObjectByType<DeparturePad>();
                    for (int i = 0; i < 4; i++)
                    {
                        var go = UnityEngine.Object.Instantiate(prefab, pad.transform.position + new Vector3(-2.25f + i * 1.5f, 0.6f, -5f), Quaternion.identity);
                        go.GetComponent<NetworkObject>().Spawn(true);
                        SyncBoxes.Add(go.GetComponent<NetworkObject>());
                    }
                } },
                new Step { Name = "제자리", Wait = 1.5f, Act = () =>
                {
                    // 상자 뒤(−z) 0.7m에서 +z(발판 반대쪽 벽 방향이 아니라 기지 안쪽)를 본다
                    for (int k = 0; k < 3; k++) MoveRemote(Clients[k], SyncBoxes[k].transform.position + new Vector3(0f, 0f, 0.7f), 180f);
                    Put(SyncBoxes[3].transform.position + new Vector3(0f, 0f, 0.7f));
                    Me().GetComponent<PlayerCameraRig>().SnapYaw(180f);
                } },
                new Step { Name = "잡기", Wait = 1f, Act = () =>
                {
                    foreach (var c in Clients) c.GetComponent<DevRemoteControl>().ServerSend("grab");
                    Carry().DevGrabNearest();
                } },
                new Step { Name = "잡음", Wait = 1f, Check = () =>
                {
                    int held = 0;
                    for (int k = 0; k < 3; k++) if (Clients[k].GetComponent<PlayerCarryController>().CarriedItemNetId.Value == SyncBoxes[k].NetworkObjectId) held++;
                    bool hostHeld = Carry().CarriedItemNetId.Value == SyncBoxes[3].NetworkObjectId;
                    Report.Append($" | 클라 잡음 {held}/3 · 호스트 {(hostHeld ? "잡음" : "못 잡음")}");
                    return held == 3 && hostHeld ? null : "다 못 잡음";
                } },
                new Step { Name = "던지기·내려놓기", Wait = 0.5f, Act = () =>
                {
                    foreach (var c in Clients) c.GetComponent<DevRemoteControl>().ServerSend("throw:0.8");
                    Carry().DevPutDown();
                } },
                new Step { Name = "멈춤 대기", Wait = 3f, Act = () =>
                {
                    HostPos.Clear();
                    var ids = new List<string>();
                    foreach (var b in SyncBoxes) { HostPos[b.NetworkObjectId] = b.transform.position; ids.Add(b.NetworkObjectId.ToString()); }
                    foreach (var c in Clients) c.GetComponent<DevRemoteControl>().ServerSend("items:" + string.Join(",", ids));
                    float moved = 0f;
                    for (int k = 0; k < 3; k++) moved += Vector3.Distance(SyncBoxes[k].transform.position, Clients[k].transform.position);
                    Report.Append($" | 던진 상자가 쥐에게서 평균 {moved / 3f:0.0}m");
                } },
                new Step { Name = "어긋남", Wait = 1.5f, Check = () =>
                {
                    var rx = new Regex(@"(\d+)=(-?[\d.]+),(-?[\d.]+),(-?[\d.]+)");
                    float worst = 0f; int seen = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        string line = null;
                        foreach (var l in File.ReadAllLines(SyncLog(i))) if (l.StartsWith("[Rat] [DevRC] items:")) line = l;
                        if (line == null) { Report.Append($" | 클라{i + 1} 기록 없음"); continue; }
                        float clientWorst = 0f;
                        foreach (Match m in rx.Matches(line))
                        {
                            ulong id = ulong.Parse(m.Groups[1].Value);
                            var p = new Vector3(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
                            if (HostPos.TryGetValue(id, out var h)) { clientWorst = Mathf.Max(clientWorst, Vector3.Distance(h, p)); seen++; }
                        }
                        worst = Mathf.Max(worst, clientWorst);
                        Report.Append($" | 클라{i + 1} 최대 어긋남 {clientWorst * 100f:0.0}cm");
                    }
                    return seen == 12 && worst < 0.1f ? null : $"어긋남 {worst * 100f:0.0}cm 또는 기록 {seen}/12";
                } },
            };
            return steps;
        }
    }
}
