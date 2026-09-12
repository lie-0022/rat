using RatGame.Core;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 무인 멀티 테스트용 — 호스트가 특정 클라의 플레이어에게 명령을 보낸다 (소유 클라에서만 실행).
    ///   grab            : 가장 가까운 물건 집기
    ///   walk:x,z        : 월드 방향으로 계속 걷기 (DevForcedInput)
    ///   stop            : 걷기 중지
    /// 협동 운반처럼 두 플레이어가 동시에 움직여야 하는 검증에 쓴다. 릴리즈엔 영향 없음(호출부 없음).
    /// </summary>
    public class DevRemoteControl : NetworkBehaviour
    {
        /// <summary>호스트 전용. 이 플레이어의 소유 클라에 명령 전송.</summary>
        public void ServerSend(string command)
        {
            if (!IsServer) return;
            DevCommandClientRpc(command, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        private void DevCommandClientRpc(string command, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            Log.Dev($"[DevRC] client {OwnerClientId} ← {command}");
            if (command == "grab")
            {
                GetComponent<PlayerCarryController>()?.DevGrabNearest();
            }
            else if (command.StartsWith("walk:"))
            {
                var parts = command.Substring(5).Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float z))
                    PlayerController.DevForcedInput = new Vector2(x, z);
            }
            else if (command == "stop")
            {
                PlayerController.DevForcedInput = Vector2.zero;
            }
        }
    }
}
