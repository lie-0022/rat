using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 씬 도착 시 전원을 그 씬의 시작 위치(PlayerSpawn 태그)로 옮긴다 (호스트 전용, docs/09·11).
    /// 플레이어 오브젝트는 씬을 넘어 유지돼 이전 씬 좌표에 남기 때문 — 스테이지 출발·기지 복귀가 같이 쓴다.
    /// </summary>
    public static class PlayerPlacement
    {
        public static void TeleportAllToSpawns()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            var points = GameObject.FindGameObjectsWithTag("PlayerSpawn");
            if (points.Length == 0) { Log.Dev("PlayerPlacement: PlayerSpawn 없음 — 제자리 유지"); return; }

            int i = 0;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var controller = client.PlayerObject.GetComponent<PlayerController>();
                if (controller == null) continue;
                var point = points[i++ % points.Length].transform;
                // 이동은 소유 클라 권한 — 소유자에게만 보내 직접 옮기게 한다
                controller.TeleportClientRpc(point.position, point.rotation, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                });
            }
        }
    }
}
