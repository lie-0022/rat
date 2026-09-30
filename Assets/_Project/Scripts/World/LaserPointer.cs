using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 레이저 포인터 (design/cat-ideas/13, 2026-09-24 고양이 41). 쥐가 손에 든 동안 조준점에 빨간 점 — 새 입력 없이 "들면 켜짐".
    /// 조준은 든 쥐의 클라가 0.1s마다 보내고(소유 클라 권한 예외와 같은 결 — 시선은 소유자만 안다), 점을 보고 쫓는 판정은 호스트.
    /// 배터리는 든 동안만 닳는다. 점이 꺼져도 고양이는 잠깐 두리번거린 뒤 돌아온다.
    /// </summary>
    [RequireComponent(typeof(CarryableItem))]
    public class LaserPointer : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<bool> On = new NetworkVariable<bool>(false);
        public NetworkVariable<Vector3> Dot = new NetworkVariable<Vector3>(Vector3.zero);

        private float _battery;
        private float _lastAimTime = -99f;
        private float _nextAttract;
        private float _nextSend;
        private Transform _dotVisual;

        /// <summary>남은 배터리 (호스트, 테스트용).</summary>
        public float Battery => _battery;

        public override void OnNetworkSpawn()
        {
            if (IsServer) _battery = _balance.LaserBatterySeconds;
            On.OnValueChanged += (_, on) => Log.Dev($"레이저 연출: {(on ? "켜짐" : "꺼짐")}"); // 2인 검증용
        }

        private void Update()
        {
            ShowDot();
            SendAimIfHeld();
            if (!IsServer || !On.Value) return;
            if (Time.time - _lastAimTime > 0.3f) { On.Value = false; return; } // 내려놓음·주머니·접속 끊김
            if (Time.time < _nextAttract) return;
            _nextAttract = Time.time + 0.2f;
            AttractCats();
        }

        // 이 물건을 손에 든 로컬 쥐만 조준을 보낸다
        private void SendAimIfHeld()
        {
            if (Time.time < _nextSend || NetworkManager == null || NetworkManager.LocalClient == null) return;
            _nextSend = Time.time + 0.1f;
            var po = NetworkManager.LocalClient.PlayerObject;
            if (po == null) return;
            var carry = po.GetComponent<PlayerCarryController>();
            if (carry == null || carry.CarriedItemNetId.Value != NetworkObjectId) return;
            var cam = Camera.main;
            if (cam == null) return;
            int mask = ~LayerMask.GetMask("Player", "Carryable", "Cat", "Ragdoll", "Ignore Raycast");
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, _balance.LaserAimRange, mask, QueryTriggerInteraction.Ignore)) return;
            AimServerRpc(hit.point);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)] // 옛 ServerRpc(RequireOwnership=false)와 같음 — NGO 2.x 권장형 (고양이 295)
        private void AimServerRpc(Vector3 point, RpcParams rpc = default)
        {
            // 보낸 쥐가 정말 이걸 들고 있나
            ulong sender = rpc.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out var client) || client.PlayerObject == null) return;
            var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
            if (carry == null || carry.CarriedItemNetId.Value != NetworkObjectId) return;
            if (_battery <= 0f) { if (On.Value) On.Value = false; return; }
            float dt = _lastAimTime > 0f ? Mathf.Min(Time.time - _lastAimTime, 0.3f) : 0.1f;
            _battery -= dt;
            _lastAimTime = Time.time;
            Dot.Value = point;
            if (!On.Value) { On.Value = true; Log.Dev($"레이저: 켜짐 (client {sender}, 배터리 {_battery:0.0}s)"); }
            if (_battery <= 0f) { On.Value = false; Log.Dev("레이저: 배터리 끝"); }
        }

        private void AttractCats()
        {
            Vector3 dot = Dot.Value;
            int blockMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker", "Default");
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                if (cat == null || !cat.IsSpawned) continue;
                if (Vector3.Distance(cat.transform.position, dot) > _balance.LaserAttractRadius) continue;
                Vector3 eye = cat.transform.position + Vector3.up * 0.5f;
                // 점이 찍힌 면에 맞는 건 보이는 것 — 점 바로 앞(0.1m)까지만 검사
                Vector3 to = dot - eye;
                if (Physics.Raycast(eye, to.normalized, to.magnitude - 0.1f, blockMask, QueryTriggerInteraction.Ignore)) continue;
                cat.ServerLaserDot(dot, _balance.LaserLingerSeconds);
            }
        }

        private void ShowDot()
        {
            if (_dotVisual == null && On.Value)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "LaserDot";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * 0.1f;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(1f, 0.05f, 0.05f));
                go.GetComponent<Renderer>().SetPropertyBlock(block);
                _dotVisual = go.transform;
            }
            if (_dotVisual == null) return;
            _dotVisual.gameObject.SetActive(On.Value);
            if (On.Value) _dotVisual.position = Dot.Value;
        }

        public override void OnDestroy()
        {
            if (_dotVisual != null) Destroy(_dotVisual.gameObject);
            base.OnDestroy();
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
