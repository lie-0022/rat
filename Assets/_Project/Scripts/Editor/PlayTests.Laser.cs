using System.Collections.Generic;
using System.IO;
using RatGame.Net;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 클라가 든 레이저 (고양이 295) — 조준 RPC를 NGO 2.x 권장형([Rpc(SendTo.Server, InvokePermission.Everyone)])으로 바꾼 뒤
    /// 주인이 아닌 클라가 보내도 호스트가 받는지. 창고: 클라 앞에 레이저 → 클라가 잡음 → 호스트에서 레이저가 켜지고 배터리가 준다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Laser Held By Client 2P (build client)")]
        private static void ArmLaser() => Arm("laser2p");

        private static LaserPointer _laser;

        private static List<Step> LaserSteps()
        {
            var steps = new List<Step>(ClientWarehouse("laser-client.log"));
            steps.AddRange(new List<Step>
            {
                new Step { Name = "레이저 앞에", Wait = 2f, Act = () =>
                {
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    var at = new Vector3(zone.center.x, zone.min.y + 0.6f, zone.center.z - 6f);
                    TeleportClient(at, 0f);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/Shop_LaserPointer.prefab");
                    var go = Object.Instantiate(prefab, at + new Vector3(0f, 0f, 0.7f), Quaternion.identity);
                    go.GetComponent<NetworkObject>().Spawn(true);
                    _laser = go.GetComponent<LaserPointer>();
                    _lastTp = 0;
                } },
                new Step { Name = "클라 잡기", Ready = () =>
                {
                    var carry = _client.GetComponent<PlayerCarryController>();
                    if (carry.CarriedItemNetId.Value == _laser.NetworkObjectId) return true;
                    if (EditorApplication.timeSinceStartup - _stepAt > 10) return true;
                    if (carry.CarriedItemNetId.Value == 0 && EditorApplication.timeSinceStartup - _lastTp > 1.5) { _lastTp = EditorApplication.timeSinceStartup; _client.GetComponent<DevRemoteControl>().ServerSend("grab!"); }
                    return false;
                }, Check = () => _client.GetComponent<PlayerCarryController>().CarriedItemNetId.Value == _laser.NetworkObjectId ? null : "클라가 레이저를 못 잡음" },
                new Step { Name = "호스트에서 켜짐", Ready = () => _laser.On.Value || EditorApplication.timeSinceStartup - _stepAt > 5, Check = () =>
                {
                    Report.Append($" | 레이저 {(_laser.On.Value ? "켜짐" : "꺼짐")}");
                    return _laser.On.Value ? null : "클라 조준이 호스트에 안 옴";
                } },
            });
            return steps;
        }
    }
}
