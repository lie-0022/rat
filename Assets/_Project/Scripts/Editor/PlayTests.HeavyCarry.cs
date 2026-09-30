using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RatGame.AI;
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
    /// 대형 2인 운반 측정 (고양이 211 — 210 측정을 버튼으로). 에디터 호스트 + macOS 빌드 클라.
    /// 기지 트인 바닥에 시험 상자(GrayBox_L 5kg·XL 10kg)를 스폰해 호스트 1인 · 클라 1인 · 2인으로 4초 옆걸음 → 물건 속도.
    /// 판단 부탁 13(대형 운반) 고친 뒤 다시 재는 용도 — 통과/실패 대신 세 속도를 보고한다(기준값이 정해지면 판정 추가).
    /// </summary>
    public static partial class PlayTests
    {
        private const string ClientApp = "Builds/macOS/Rat.app";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static NetworkObject _client;
        private static CarryableItem _heavy;
        private static bool _longX;
        private static Vector3 _p0;
        private static double _joinedAt;

        [MenuItem("Tools/RatGame/Test/Heavy Carry 2P (build client)")]
        private static void ArmHeavy() => Arm("heavy2p");

        private static List<Step> HeavyCarrySteps()
        {
            var steps = new List<Step>
            {
                new Step { Name = "호스트", Wait = 1f, Ready = () => UnityEngine.Object.FindFirstObjectByType<MainMenuController>() != null, Act = () =>
                {
                    var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                    var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
                } },
                new Step { Name = "빌드 클라", Wait = 2f, Ready = () => SceneManager.GetActiveScene().name == "Hub", Act = () =>
                {
                    if (!Directory.Exists(ClientApp)) throw new Exception($"빌드 없음 ({ClientApp})");
                    System.Diagnostics.Process.Start("open", $"-n {ClientApp} --args -unitytransport{DevPort.ClientArg} -autojoin -logFile \"{Path.GetFullPath("Temp/heavy-client.log")}\"");
                    OnDone = () => System.Diagnostics.Process.Start("pkill", "-f Rat.app/Contents/MacOS");
                    _joinedAt = 0;
                } },
                // 붙은 직후 순간이동은 클라 스폰 전이라 사라진다 — 3초 뒤부터 (고양이 201)
                new Step { Name = "클라 접속", Ready = () =>
                {
                    var nm = NetworkManager.Singleton;
                    if (nm.ConnectedClientsIds.Count < 2) return false;
                    if (_joinedAt == 0) _joinedAt = EditorApplication.timeSinceStartup;
                    return EditorApplication.timeSinceStartup - _joinedAt > 3;
                }, Act = () =>
                {
                    foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                        if (id != NetworkManager.Singleton.LocalClientId) _client = NetworkManager.Singleton.ConnectedClients[id].PlayerObject;
                } },
            };
            // 생성 맵은 물건 모양·주변 지형·잡는 자리가 판마다 달라 흔들렸다 (211 첫 판들: 0.00~0.80) —
            // 기지 트인 바닥에 정육면체 시험 상자를 스폰해 잰다. L 5kg·XL 10kg (XXL 20kg은 대형 분류가 아니라 제외 — 판단 13 참고)
            foreach (var box in new[] { "GrayBox_L", "GrayBox_XL" })
            {
                steps.Add(new Step { Name = box + " 스폰", Wait = 1f, Act = () =>
                {
                    if (_heavy != null && _heavy.IsSpawned) _heavy.NetworkObject.Despawn();
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Items/{box}.prefab");
                    var pad = UnityEngine.Object.FindFirstObjectByType<DeparturePad>();
                    var go = UnityEngine.Object.Instantiate(prefab, pad.transform.position + new Vector3(0f, 1f, -5f) /* 발판에서 멀리 — 둘이 발판에 서면 출발해 버린다 */, Quaternion.identity);
                    go.GetComponent<NetworkObject>().Spawn(true);
                    _heavy = go.GetComponent<CarryableItem>();
                } });
                steps.Add(new Step { Name = box + " 안정", Wait = 1.5f, Act = () =>
                {
                    var b = _heavy.GetComponentInChildren<Collider>().bounds;
                    _longX = b.size.x >= b.size.z;
                    Report.Append($" | {_heavy.name.Replace("(Clone)", "")} {_heavy.Mass}kg");
                } });
                steps.AddRange(Phase("호스트 1인", true, false));
                steps.AddRange(Phase("클라 1인", false, true));
                steps.AddRange(Phase("2인", true, true));
            }
            return steps;
        }

        // 잡는 자리(짧은 축 양쪽)에 서서 긴 축 방향 옆걸음 4초 — 앞뒤로 걸으면 뒤 쥐가 물건에 부딪힌다 (210 첫 측정)
        private static IEnumerable<Step> Phase(string name, bool host, bool client)
        {
            yield return new Step { Name = name + " 자리", Wait = 1f, Act = () =>
            {
                if (host) { Put(Side(false) + Vector3.up * 0.6f); Me().GetComponent<PlayerCameraRig>().SnapYaw(_longX ? 0f : 90f); }
                if (client) TeleportClient(Side(true) + Vector3.up * 0.6f, _longX ? 180f : 270f);
            } };
            yield return new Step { Name = name + " 잡기", Wait = 0.8f, Act = () =>
            {
                if (host) Carry().DevGrabNearest();
                if (client) _client.GetComponent<DevRemoteControl>().ServerSend("grab");
            } };
            yield return new Step { Name = name + " 걷기", Wait = 1f, Act = () =>
            {
                int want = (host ? 1 : 0) + (client ? 1 : 0);
                if (_heavy.CarrierIds.Count != want) Fail($"{name}: 잡은 쥐 {_heavy.CarrierIds.Count}/{want}");
                _p0 = _heavy.transform.position;
                // 잡은 뒤 실제 쥐 자리 기준 — 두 쥐를 잇는 선(1인이면 쥐-물건 선)에 수직으로 걸어야 서로·물건에 안 부딪힌다.
                // 겉모양 상자로 긴 축을 고르면 둥근 물건(치즈 덩어리)에서 자리와 어긋났다 (고양이 211 셋째 판 2인 0.00)
                Vector3 a = host ? Me().transform.position : _client.transform.position;
                Vector3 bpos = host && client ? _client.transform.position : _heavy.transform.position;
                Vector3 line = bpos - a; line.y = 0f;
                Vector3 axis = Vector3.Cross(Vector3.up, line.normalized);
                var along = FreeDirection(axis);
                if (host) PlayerController.DevForcedInput = along;
                if (client) _client.GetComponent<DevRemoteControl>().ServerSend(string.Format(Inv, "walk:{0},{1}", along.x, along.y));
            } };
            yield return new Step { Name = name + " 측정", Wait = 4f, Act = () =>
            {
                var d = _heavy.transform.position - _p0; d.y = 0f;
                Report.Append($" | {name} {d.magnitude / 4f:0.00} m/s");
                PlayerController.DevForcedInput = Vector2.zero;
                _client.GetComponent<DevRemoteControl>().ServerSend("stop");
                Carry().ServerForceDrop();
                _client.GetComponent<PlayerCarryController>().ServerForceDrop();
            } };
        }

        // 긴 축 양쪽 중 더 트인 쪽 — 벽·물건에 막힌 쪽으로 걸으면 0이 나온다 (고양이 211 첫 판: 모두 0.00)
        private static Vector2 FreeDirection(Vector3 axis)
        {
            var b = _heavy.GetComponentInChildren<Collider>().bounds;
            float Free(Vector3 dir)
            {
                float best = 8f;
                foreach (var hit in Physics.RaycastAll(b.center, dir, 8f, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.transform.IsChildOf(_heavy.transform) && hit.rigidbody == null) best = Mathf.Min(best, hit.distance);
                return best;
            }
            float plus = Free(axis), minus = Free(-axis);
            Report.Append($" | 트인 거리 +{plus:0.0}m −{minus:0.0}m");
            var d = plus >= minus ? axis : -axis;
            return new Vector2(d.x, d.z);
        }

        private static Vector3 Side(bool plus)
        {
            var b = _heavy.GetComponentInChildren<Collider>().bounds;
            return _longX ? new Vector3(b.center.x, b.min.y, plus ? b.max.z + 0.4f : b.min.z - 0.4f)
                          : new Vector3(plus ? b.max.x + 0.4f : b.min.x - 0.4f, b.min.y, b.center.z);
        }

        // 클라 쥐는 소유 클라 권한 — 호스트가 옮기면 곧 덮인다, RPC로 (docs/03)
        private static void TeleportClient(Vector3 pos, float yaw)
        {
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { _client.OwnerClientId } } };
            _client.GetComponent<DevRemoteControl>().ServerSend(string.Format(Inv, "tp:{0},{1},{2}", pos.x, pos.y, pos.z));
            _client.GetComponent<PlayerController>().FaceClientRpc(yaw, target);
        }
    }
}
