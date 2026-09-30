using RatGame.Core;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 무인 멀티 테스트용 — 호스트가 특정 클라의 플레이어에게 명령을 보낸다 (소유 클라에서만 실행).
    ///   grab            : 가장 가까운 물건 집기
    ///   throw:차지      : 들고 있는 것 던지기 (0~1, 시선 방향 — 고양이 236)
    ///   drop            : 내려놓기
    ///   items:id,id…    : 이 클라에서 보이는 그 네트 오브젝트들의 위치 로그 (동기화 어긋남 재기 — 고양이 236)
    ///   ping            : 카메라 정면으로 핑
    ///   sniff           : 킁킁 (목적지 냄새 줄기)
    ///   squeak          : 찍찍 (동료 자막, 고양이 158)
    ///   walk:x,z        : 월드 방향으로 계속 걷기 (DevForcedInput)
    ///   stop            : 걷기 중지
    ///   crouch / stand  : 웅크리기 켜기·끄기 (DevForcedCrouch, 쥐덫 미끼 검증 — 고양이 129)
    ///   report          : 이 클라에서 보이는 모든 쥐의 위치·키(스케일)·고양이 배율 로그 (고양이 137·141)
    ///   trace:초        : 이 클라에서 호스트 쥐 움직임을 매 프레임 재서 요약 — 프레임 간 최대 이동·튐 횟수 (보간 확인, 고양이 137)
    ///   tp:x,y,z        : 소유 클라에서 순간이동 (InvariantCulture 소수점)
    ///   hud:이름        : 이름에 그 낱말이 든 UI 아래 켜진 글자를 로그 (클라 화면 글자 확인, 고양이 182 — 전체 화면 찍기 대신)
    /// 협동 운반처럼 두 플레이어가 동시에 움직여야 하는 검증에 쓴다. 릴리스 빌드는 명령을 무시한다.
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
            // 릴리스에선 받지 않는다 — 고친 호스트가 친구 쥐를 조종하지 못하게 (고양이 322). 에디터·개발 빌드(시험 도구)만
            if (!IsOwner || !Debug.isDebugBuild) return;
            Log.Dev($"[DevRC] client {OwnerClientId} ← {command}");
            if (command == "grab")
            {
                GetComponent<PlayerCarryController>()?.DevGrabNearest();
            }
            else if (command == "grab!") // 손이 빌 때만 잡기 — 다시 보내도 든 걸 놓지 않게(잡기는 누를 때마다 들기/놓기, 고양이 286)
            {
                var carry = GetComponent<PlayerCarryController>();
                if (carry != null && !carry.IsHolding) carry.DevGrabNearest();
            }
            else if (command.StartsWith("throw:") && float.TryParse(command.Substring(6), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float charge))
            {
                GetComponent<PlayerCarryController>()?.DevThrow(charge);
            }
            else if (command.StartsWith("slot:") && int.TryParse(command.Substring(5), out int slot))
            {
                var carry = GetComponent<PlayerCarryController>(); // 칸 전환 — 든 물건은 주머니로 (고양이 264 이탈 시험)
                if (carry != null) carry.SelectedSlot.Value = slot;
            }
            else if (command == "leave") // 일시정지 창 "세션 나가기"와 같은 길 — 클라가 스스로 나감 (고양이 299 재접속 시험)
            {
                var launcher = FindFirstObjectByType<NetworkLauncher>();
                if (launcher != null) launcher.Shutdown();
            }
            else if (command == "drop")
            {
                GetComponent<PlayerCarryController>()?.DevPutDown();
            }
            else if (command.StartsWith("items:"))
            {
                var sb = new System.Text.StringBuilder("[DevRC] items:");
                foreach (var part in command.Substring(6).Split(','))
                    if (ulong.TryParse(part, out ulong id) && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj))
                        sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, " {0}={1:F3},{2:F3},{3:F3}", id, obj.transform.position.x, obj.transform.position.y, obj.transform.position.z));
                Log.Dev(sb.ToString());
            }
            else if (command == "ping")
            {
                GetComponent<PlayerPing>()?.TryPing(); // 카메라 정면 핑 (실제 입력과 같은 경로)
            }
            else if (command == "sniff")
            {
                GetComponent<PlayerSniff>()?.TrySniff();
            }
            else if (command == "squeak")
            {
                GetComponent<Noise.PlayerNoiseEmitter>()?.Squeak(); // 동료 찍찍 자막 검증 (고양이 158)
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
                foreach (var cat in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None)) // 큰 고양이 크기가 클라에도 갔나 (고양이 141)
                    Log.Dev($"[DevRC] report: 고양이 {cat.name} 위치 {cat.transform.position:F1} 배율 {cat.transform.localScale.x:F2}");
                var zones = FindObjectsByType<World.LightZone>(FindObjectsSortMode.None); // 어둠 구역 크기가 클라에도 갔나 (고양이 147)
                if (zones.Length > 0) Log.Dev($"[DevRC] report: 어둠 구역 {zones.Length}개, 첫 배율 {zones[0].transform.localScale.x:F2}");
            }
            else if (command.StartsWith("trace:") && float.TryParse(command.Substring(6), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float secs))
            {
                StartCoroutine(TraceHost(secs));
            }
            else if (command.StartsWith("hud:"))
            {
                string key = command.Substring(4);
                foreach (var label in FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
                {
                    if (!label.isActiveAndEnabled || !UnderNamed(label.transform, key)) continue;
                    Log.Dev($"[DevRC] hud {label.name}: {label.text}");
                }
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

        private static bool UnderNamed(Transform t, string key)
        {
            for (; t != null; t = t.parent)
                if (t.name.Contains(key)) return true;
            return false;
        }
    }
}
