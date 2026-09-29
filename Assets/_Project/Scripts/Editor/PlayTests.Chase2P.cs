using System.Collections.Generic;
using System.IO;
using RatGame.AI;
using RatGame.Player;
using RatGame.World;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 고양이 추격 2인 (고양이 237) — 빌드 클라 쥐가 쫓기고 잡힌다. 호스트(판정)에서 추격 → 포획(동료가 있으면 가지고 놀기 = Pinned),
    /// 클라 로그의 팀 상태 연출에 자기 쥐가 잡힌 상태로 보였는지. 호스트 쥐는 멀리 둔다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Cat Chase 2P (build client)")]
        private static void ArmChase2P() => Arm("chase2p");

        private static double _lastTp;

        private static List<Step> Chase2PSteps()
        {
            var steps = new List<Step>(ClientWarehouse("chase-client.log"));
            steps.AddRange(new List<Step>
            {
                new Step { Name = "고양이 깨우기", Wait = 3f, Act = () =>
                {
                    Transitions.Clear();
                    CatBrain.ServerStateChanged -= OnCat;
                    CatBrain.ServerStateChanged += OnCat;
                    Cat().ServerWake();
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    Put(zone.center + new Vector3(0f, 0.2f, 0f)); // 호스트는 쥐구멍 안 — 안 잡히게, 귀환은 둘 다 모여야라 안 된다
                } },
                // 클라 쥐를 고양이 앞 1.5m에 — 순간이동은 클라가 하므로 0.5초마다 다시 보낸다
                new Step { Name = "클라 추격당함", Ready = () =>
                {
                    var client = _client.GetComponent<PlayerCondition>();
                    if (client.State.Value != ConditionState.Active) return true;
                    if (EditorApplication.timeSinceStartup - _stepAt > 40) return true;
                    var st = Cat().State.Value;
                    if ((st is CatState.Patrol or CatState.Suspicious or CatState.Return or CatState.Sleep) && EditorApplication.timeSinceStartup - _lastTp > 0.5)
                    {
                        _lastTp = EditorApplication.timeSinceStartup;
                        var fwd = Cat().transform.forward; fwd.y = 0f; fwd.Normalize();
                        TeleportClient(Cat().transform.position + fwd * 1.5f + Vector3.up * 0.4f, 0f);
                    }
                    return false;
                }, Check = () =>
                {
                    var st = _client.GetComponent<PlayerCondition>().State.Value;
                    Report.Append($" | 고양이 {string.Join(",", Transitions)} · 클라 {st}");
                    return st is ConditionState.Pinned or ConditionState.Downed ? null : "클라가 안 잡힘";
                } },
                new Step { Name = "클라 화면", Wait = 1.5f, Act = () => CatBrain.ServerStateChanged -= OnCat, Check = () =>
                {
                    string seen = null;
                    foreach (var l in File.ReadAllLines(Path.GetFullPath("Temp/chase-client.log")))
                        if (l.StartsWith("[Rat] 팀 상태 연출") && (l.Contains("1 Pinned") || l.Contains("1 Downed"))) seen = l;
                    Report.Append($" | 클라 로그: {(seen ?? "없음")}");
                    return seen != null ? null : "클라 화면에 잡힌 상태가 안 보임";
                } },
            });
            return steps;
        }
    }
}
