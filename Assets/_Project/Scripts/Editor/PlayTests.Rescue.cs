using System;
using System.Collections.Generic;
using System.IO;
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
    /// 쓰러진 동료 구조 2인 (고양이 214 — 212·213 대형 자리 변경 회귀). 에디터 호스트 + macOS 빌드 클라, 창고 맵.
    /// 클라를 쓰러뜨림 → 대리 몸(DownedBody, 대형 3kg)이 생김 → 호스트가 몸을 잡아 쥐구멍 쪽으로 끌기 → 쥐구멍에 닿으면 클라가 살아나는지.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Downed Rescue 2P (build client)")]
        private static void ArmRescue() => Arm("rescue2p");

        private static DownedBody _body;
        private static float _bodyStartDist;

        // 2인 창고까지 (호스트 → 빌드 클라 접속 → 둘 다 발판 → 창고) — 구조·끈끈이 시험 공용 (고양이 226)
        private static IEnumerable<Step> ClientWarehouse(string logName)
        {
            Action restoreSave = null; // 구조 통계(도전과제)가 세이브를 바꾼다 — 떠 두고 되돌림 (고양이 234)
            yield return new Step { Name = "호스트", Wait = 1f, Ready = () => UnityEngine.Object.FindFirstObjectByType<MainMenuController>() != null, Act = () =>
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
            } };
            yield return new Step { Name = "빌드 클라", Wait = 2f, Ready = () => SceneManager.GetActiveScene().name == "Hub", Act = () =>
            {
                if (!Directory.Exists(ClientApp)) throw new Exception($"빌드 없음 ({ClientApp})");
                System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport{DevPort.ClientArg} -autojoin -logFile \"{Path.GetFullPath($"Temp/{logName}")}\"");
                restoreSave = BackupSave(logName);
                OnDone = () => { System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS"); restoreSave(); };
                _joinedAt = 0;
            } };
            yield return new Step { Name = "클라 접속", Ready = () =>
            {
                if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return false;
                if (_joinedAt == 0) _joinedAt = EditorApplication.timeSinceStartup;
                return EditorApplication.timeSinceStartup - _joinedAt > 3;
            }, Act = () =>
            {
                foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                    if (id != NetworkManager.Singleton.LocalClientId) _client = NetworkManager.Singleton.ConnectedClients[id].PlayerObject;
            } };
            yield return new Step { Name = "창고 도착", Ready = () =>
            {
                if (SceneManager.GetActiveScene().name == "Stage_Warehouse01") return UnityEngine.Object.FindFirstObjectByType<DepositZone>() != null;
                var pad = UnityEngine.Object.FindFirstObjectByType<DeparturePad>();
                if (pad != null && EditorApplication.timeSinceStartup - _stepAt > 1)
                {
                    _stepAt = EditorApplication.timeSinceStartup - 0.5;
                    Put(pad.transform.position + Vector3.up * 0.5f);
                    TeleportClient(pad.transform.position + new Vector3(0.6f, 0.5f, 0f), 0f);
                }
                return false;
            } };
        }

        private static List<Step> RescueSteps()
        {
            var steps = new List<Step>(ClientWarehouse("rescue-client.log"));
            steps.AddRange(RescueBody());
            return steps;
        }

        private static List<Step> RescueBody() => new()
        {
            new Step { Name = "클라 쓰러뜨림", Wait = 3f, Act = () =>
            {
                // 쥐구멍에서 6m쯤 떨어진 곳에서 쓰러뜨려야 "끌어서 살리기"가 된다
                var zone = UnityEngine.Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                Vector3 away = zone.center + new Vector3(0f, 0f, -6f);
                TeleportClient(new Vector3(away.x, zone.min.y + 0.6f, away.z), 0f);
            } },
            new Step { Name = "쓰러짐", Wait = 1f, Act = () => _client.GetComponent<PlayerCondition>().ServerSetState(ConditionState.Downed) },
            new Step { Name = "대리 몸", Wait = 1f, Check = () =>
            {
                _body = null;
                foreach (var b in UnityEngine.Object.FindObjectsByType<DownedBody>(FindObjectsSortMode.None))
                    if (b.Owner.Value == _client.OwnerClientId) _body = b;
                if (_body == null) return "대리 몸이 안 생김";
                var item = _body.GetComponent<CarryableItem>();
                Report.Append($" | 몸 {item.Mass}kg 대형={item.IsHeavy} 자리 {item.CarrySlotCount}");
                return null;
            } },
            new Step { Name = "몸 잡기", Act = () => { Front(_body.GetComponent<CarryableItem>(), 0.45f); } },
            new Step { Name = "몸 잡기", Wait = 0.5f, Act = () => Carry().DevGrabNearest(),
            },
            new Step { Name = "몸 잡음", Wait = 0.6f, Check = () =>
            {
                if (Carry().CarriedItem != _body.GetComponent<CarryableItem>()) return $"손에 {Carry().CarriedItem?.name ?? "없음"}";
                Report.Append($" | 호스트 높이 {Me().transform.position.y:0.00}"); // 뜨면 안 된다 (고양이 213)
                return Me().transform.position.y < 1f ? null : "호스트 쥐가 떠 있음";
            }, Act = () =>
            {
                var zone = UnityEngine.Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                Vector3 to = zone.center - Me().transform.position; to.y = 0f;
                _bodyStartDist = to.magnitude;
                var d = to.normalized;
                PlayerController.DevForcedInput = new Vector2(d.x, d.z);
            } },
            // 쥐구멍까지 끌고 가 살아날 때까지 (최대 20초) — 가는 동안 방향을 다시 잡는다
            new Step { Name = "살아남", Ready = () =>
            {
                if (_client.GetComponent<PlayerCondition>().State.Value == ConditionState.Active) return true;
                if (EditorApplication.timeSinceStartup - _stepAt > 20) return true;
                var zone = UnityEngine.Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                Vector3 to = zone.center - Me().transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) { var d = to.normalized; PlayerController.DevForcedInput = new Vector2(d.x, d.z); }
                return false;
            }, Check = () =>
            {
                PlayerController.DevForcedInput = Vector2.zero;
                var st = _client.GetComponent<PlayerCondition>().State.Value;
                Report.Append($" | 끈 거리 {_bodyStartDist:0.0}m · {EditorApplication.timeSinceStartup - _stepAt:0.0}초 · 클라 {st}");
                return st == ConditionState.Active ? null : "20초 안에 못 살림";
            } },
        };
    }
}
