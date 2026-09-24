using RatGame.Core;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 무인 멀티 테스트용 — 호스트가 특정 클라의 플레이어에게 명령을 보낸다 (소유 클라에서만 실행).
    ///   grab            : 가장 가까운 물건 집기
    ///   ping            : 카메라 정면으로 핑
    ///   sniff           : 킁킁 (목적지 냄새 줄기)
    ///   walk:x,z        : 월드 방향으로 계속 걷기 (DevForcedInput)
    ///   stop            : 걷기 중지
    ///   crouch / stand  : 웅크리기 켜기·끄기 (DevForcedCrouch, 쥐덫 미끼 검증 — 고양이 129)
    ///   report          : 이 클라에서 보이는 모든 쥐의 위치·키(스케일) 로그 (고양이 137)
    ///   trace:초        : 이 클라에서 호스트 쥐 움직임을 매 프레임 재서 요약 — 프레임 간 최대 이동·튐 횟수 (보간 확인, 고양이 137)
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
            else if (command == "ping")
            {
                GetComponent<PlayerPing>()?.TryPing(); // 카메라 정면 핑 (실제 입력과 같은 경로)
            }
            else if (command == "sniff")
            {
                GetComponent<PlayerSniff>()?.TrySniff();
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
            else if (command == "crouch" || command == "stand")
            {
                PlayerController.DevForcedCrouch = command == "crouch";
            }
            else if (command == "report")
            {
                foreach (var p in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None))
                    Log.Dev($"[DevRC] report: client {p.OwnerClientId} 위치 {p.transform.position:F2} 키 {p.transform.localScale.y:F2} 상태 {p.State.Value}");
            }
            else if (command.StartsWith("trace:") && float.TryParse(command.Substring(6), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float secs))
            {
                StartCoroutine(TraceHost(secs));
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

        // 원격(호스트) 쥐가 이 화면에서 끊겨 보이는지 — 매 프레임 이동량. 튐 = 한 프레임에 0.5m 넘게
        private System.Collections.IEnumerator TraceHost(float seconds)
        {
            Transform host = null;
            foreach (var p in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None)) if (p.OwnerClientId == 0) host = p.transform;
            if (host == null) { Log.Dev("[DevRC] trace: 호스트 쥐 없음"); yield break; }
            Vector3 last = host.position; float maxStep = 0f, total = 0f, maxY = last.y; int frames = 0, pops = 0, still = 0;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
                float step = Vector3.Distance(host.position, last);
                maxStep = Mathf.Max(maxStep, step); total += step; frames++;
                if (step > 0.5f) pops++;
                if (step < 0.0001f) still++;
                maxY = Mathf.Max(maxY, host.position.y);
                last = host.position;
            }
            Log.Dev($"[DevRC] trace: {frames}프레임, 이동 {total:F2}m, 프레임 최대 {maxStep:F3}m, 튐 {pops}, 멈춘 프레임 {still}, 최고 y {maxY:F2}");
        }
    }
}
