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
    ///   tp:x,y,z        : 소유 클라에서 순간이동 (InvariantCulture 소수점)
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
            else if (command.StartsWith("tp:"))
            {
                // 소유 클라에서 직접 이동 — 호스트가 원격 플레이어를 옮기면 소유자 위치가 곧 덮어써서 테스트가 거짓이 된다
                var p = command.Substring(3).Split(',');
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var style = System.Globalization.NumberStyles.Float;
                if (p.Length == 3 && float.TryParse(p[0], style, inv, out float tx)
                    && float.TryParse(p[1], style, inv, out float ty) && float.TryParse(p[2], style, inv, out float tz))
                {
                    var pos = new Vector3(tx, ty, tz);
                    var rb = GetComponent<Rigidbody>();
                    if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
                    transform.position = pos;
                }
            }
        }
    }
}
