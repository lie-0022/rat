using System;
using System.Collections.Generic;
using System.IO;
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
    /// 특대 4인 운반 (고양이 212 — docs/05 "특대 16~24kg, grip 4, 권장 3~4"). 에디터 호스트 + macOS 빌드 클라 3개.
    /// 기지 바닥에 GrayBox_XXL(20kg)을 스폰 → 자리 수 4 확인 → 네 명이 차례로 잡기 → 긴 축 방향으로 함께 4초 → 물건 속도.
    /// 2인·3인도 같은 물건으로 재서 "인원이 늘면 빨라짐"을 본다.
    /// </summary>
    public static partial class PlayTests
    {
        private static readonly List<NetworkObject> Clients = new();

        [MenuItem("Tools/RatGame/Test/Extra Large Carry 4P (3 build clients)")]
        private static void ArmExtraLarge() => Arm("xl4p");

        private static List<Step> ExtraLargeSteps()
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
                        System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport -autojoin -logFile \"{Path.GetFullPath($"Temp/xl-client{i + 1}.log")}\"");
                    OnDone = () => System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS");
                    _joinedAt = 0;
                } },
                new Step { Name = "클라 접속", Ready = () =>
                {
                    if (NetworkManager.Singleton.ConnectedClientsIds.Count < 4) return false;
                    if (_joinedAt == 0) _joinedAt = EditorApplication.timeSinceStartup;
                    return EditorApplication.timeSinceStartup - _joinedAt > 3; // 스폰 직후 순간이동은 사라진다 (고양이 201)
                }, Act = () =>
                {
                    Clients.Clear();
                    var nm = NetworkManager.Singleton;
                    foreach (var id in nm.ConnectedClientsIds) if (id != nm.LocalClientId) Clients.Add(nm.ConnectedClients[id].PlayerObject);
                } },
                new Step { Name = "XXL 스폰", Wait = 1f, Act = () =>
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/GrayBox_XXL.prefab");
                    var pad = UnityEngine.Object.FindFirstObjectByType<DeparturePad>();
                    var go = UnityEngine.Object.Instantiate(prefab, pad.transform.position + new Vector3(0f, 1f, -5.5f), Quaternion.identity);
                    go.GetComponent<NetworkObject>().Spawn(true);
                    _heavy = go.GetComponent<CarryableItem>();
                } },
                new Step { Name = "자리 수", Wait = 1.5f, Check = () =>
                {
                    Report.Append($" | {_heavy.Mass}kg 대형={_heavy.IsHeavy} 자리 {_heavy.CarrySlotCount}");
                    return _heavy.IsHeavy && _heavy.CarrySlotCount == 4 ? null : "특대인데 자리가 4가 아님";
                } },
            };
            steps.AddRange(Group(2));
            steps.AddRange(Group(3));
            steps.AddRange(Group(4));
            return steps;
        }

        // n명(호스트 + 클라 n−1)이 잡고 함께 걷기. 자리는 긴 변 양쪽 둘씩 → 긴 축 방향으로 걸어야 서로·물건에 안 부딪힌다
        private static IEnumerable<Step> Group(int n)
        {
            yield return new Step { Name = $"{n}인 자리", Wait = 1f, Act = () =>
            {
                var b = _heavy.GetComponentInChildren<Collider>().bounds;
                bool longZ = b.size.z >= b.size.x;
                // 자리 순서와 같게: +쪽 앞, −쪽 앞, +쪽 뒤, −쪽 뒤 (CarryableItem.Slots)
                Vector3 Spot(int i)
                {
                    float sign = i % 2 == 0 ? 1f : -1f, along = i < 2 ? 0.25f : -0.25f;
                    return longZ ? new Vector3(sign > 0 ? b.max.x + 0.4f : b.min.x - 0.4f, b.min.y + 0.6f, b.center.z + along * b.size.z)
                                 : new Vector3(b.center.x + along * b.size.x, b.min.y + 0.6f, sign > 0 ? b.max.z + 0.4f : b.min.z - 0.4f);
                }
                float Yaw(int i) => longZ ? (i % 2 == 0 ? 270f : 90f) : (i % 2 == 0 ? 180f : 0f);
                Put(Spot(0)); Me().GetComponent<PlayerCameraRig>().SnapYaw(Yaw(0));
                for (int k = 0; k < n - 1; k++) MoveRemote(Clients[k], Spot(k + 1), Yaw(k + 1));
            } };
            yield return new Step { Name = $"{n}인 잡기", Wait = 0.8f, Act = () =>
            {
                Carry().DevGrabNearest();
                for (int k = 0; k < n - 1; k++) Clients[k].GetComponent<DevRemoteControl>().ServerSend("grab");
            } };
            yield return new Step { Name = $"{n}인 걷기", Wait = 1f, Act = () =>
            {
                if (_heavy.CarrierIds.Count != n) Fail($"{n}인: 잡은 쥐 {_heavy.CarrierIds.Count}/{n}");
                var b = _heavy.GetComponentInChildren<Collider>().bounds;
                var axis = b.size.z >= b.size.x ? Vector3.forward : Vector3.right;
                var along = FreeDirection(axis);
                _p0 = _heavy.transform.position;
                PlayerController.DevForcedInput = along;
                for (int k = 0; k < n - 1; k++) Clients[k].GetComponent<DevRemoteControl>().ServerSend(string.Format(Inv, "walk:{0},{1}", along.x, along.y));
            } };
            // 조사용 추적 (고양이 213): 0.5초마다 물건·쥐 위치와 관절 힘 — 여럿이 들어도 0 m/s인 원인 찾기
            for (int k = 0; k < 7; k++)
            {
                int sample = k;
                yield return new Step { Name = $"{n}인 추적", Wait = 0.5f, Act = () =>
                {
                    var sb = new System.Text.StringBuilder($"[Rat] 특대 추적 {n}인 t{(sample + 1) * 0.5f:0.0}: 물건 {_heavy.transform.position:F2} v {_heavy.GetComponent<Rigidbody>().linearVelocity.magnitude:0.00} · 호스트 {Me().transform.position:F2}");
                    foreach (var c in Clients) sb.Append($" · c{c.OwnerClientId} {c.transform.position:F2}");
                    foreach (var j in _heavy.GetComponents<ConfigurableJoint>())
                        sb.Append($" · 관절→{(j.connectedBody != null ? j.connectedBody.name : "없음")} 힘 {j.currentForce.magnitude:0} 키네 {(j.connectedBody != null && j.connectedBody.isKinematic)}");
                    Debug.Log(sb.ToString());
                } };
            }
            yield return new Step { Name = $"{n}인 측정", Wait = 0.5f, Act = () =>
            {
                var d = _heavy.transform.position - _p0; d.y = 0f;
                Report.Append($" | {n}인 {d.magnitude / 4f:0.00} m/s");
                PlayerController.DevForcedInput = Vector2.zero;
                Carry().ServerForceDrop();
                foreach (var c in Clients) { c.GetComponent<DevRemoteControl>().ServerSend("stop"); c.GetComponent<PlayerCarryController>().ServerForceDrop(); }
            } };
        }

        private static void MoveRemote(NetworkObject client, Vector3 pos, float yaw)
        {
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { client.OwnerClientId } } };
            client.GetComponent<DevRemoteControl>().ServerSend(string.Format(Inv, "tp:{0},{1},{2}", pos.x, pos.y, pos.z));
            client.GetComponent<PlayerController>().FaceClientRpc(yaw, target);
        }
    }
}
