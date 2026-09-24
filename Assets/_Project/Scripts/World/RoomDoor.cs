using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.World
{
    /// <summary>
    /// 방 문 (design/cat-ideas/09, 2026-09-24 고양이 40). 쥐가 E로 닫고 연다. 닫히면 문판의 NavMeshObstacle이 고양이 길을 막고,
    /// 쫓거나 조사하던 고양이는 문 앞에서 몸통으로 밀어 4s 뒤 연다 — 도망 시간. 순찰 고양이는 안 민다.
    /// 판정은 호스트, 문판 회전·덜컹은 Open·PushProgress NV로 전 클라.
    /// </summary>
    public class RoomDoor : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private Transform _hinge;            // 문판 경첩 — 열리면 90° 돈다
        [SerializeField] private NavMeshObstacle _obstacle;   // 문판의 carve 장애물 — 닫혔을 때만 켠다
        [SerializeField] private Vector3 _roomCenter;         // 이 문이 닫는 방 (월드 XZ 박스) — 목적지가 방 안이면 문으로 돌린다
        [SerializeField] private Vector3 _roomSize;

        private static readonly List<RoomDoor> _all = new();

        /// <summary>
        /// 고양이 목적지 보정 (CatMovement.MoveTo): 닫힌 문 너머(방 안↔밖)로 가려 하면 문 앞(내 쪽)으로.
        /// 안 그러면 NavMesh가 "가장 가까운 닿는 곳"(대개 방 벽 바깥)으로 보내 문을 밀 기회가 없다.
        /// </summary>
        public static Vector3 Redirect(Vector3 from, Vector3 to)
        {
            foreach (var d in _all)
            {
                if (d == null || d.Open.Value) continue;
                bool fromIn = d.InRoom(from), toIn = d.InRoom(to);
                if (fromIn == toIn) continue;
                Vector3 f = d.transform.forward; f.y = 0f; // forward = 방 밖
                return d.transform.position + f.normalized * (fromIn ? -0.8f : 0.8f);
            }
            return to;
        }

        private bool InRoom(Vector3 p) =>
            Mathf.Abs(p.x - _roomCenter.x) <= _roomSize.x * 0.5f && Mathf.Abs(p.z - _roomCenter.z) <= _roomSize.z * 0.5f;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public NetworkVariable<bool> Open = new NetworkVariable<bool>(true);
        public NetworkVariable<float> PushProgress = new NetworkVariable<float>(0f);

        private float _pushed;
        private float _nextCheck;
        private Quaternion _closedRot;
        private bool _wasPushing;

        public string PromptText => Open.Value ? "문 닫기" : "문 열기";
        public float HoldSeconds => 0.4f;
        public bool CanInteract(ulong clientId) => Open.Value ? !CatInDoorway(1f) : true;

        private void Awake() { if (_hinge != null) _closedRot = _hinge.localRotation; }

        public override void OnNetworkSpawn()
        {
            Open.OnValueChanged += (_, open) => { ApplyOpen(); Log.Dev($"문 연출: {(open ? "열림" : "닫힘")}"); }; // 2인 검증용
            ApplyOpen();
        }

        private void ApplyOpen()
        {
            if (_obstacle != null) _obstacle.enabled = !Open.Value;
        }

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer) return;
            bool closing = Open.Value;
            if (closing && CatInDoorway(1f)) return;
            Open.Value = !Open.Value;
            _pushed = 0f;
            PushProgress.Value = 0f;
            if (closing) NoiseSystem.Emit(transform.position, _balance.DoorCloseNoise, NoiseType.Impact, clientId); // 쾅 — 고양이가 듣는다
            Log.Dev($"문: client {clientId}가 {(closing ? "닫음" : "엶")}");
        }

        private void Update()
        {
            // 연출 (모든 클라): 열리면 90°, 밀리는 중이면 덜컹
            if (_hinge != null)
            {
                float jiggle = !Open.Value && PushProgress.Value > 0f ? Mathf.Sin(Time.time * 25f) * 6f * PushProgress.Value : 0f;
                _hinge.localRotation = _closedRot * Quaternion.Euler(0f, Open.Value ? 90f : jiggle, 0f);
            }
            bool pushing = !Open.Value && PushProgress.Value > 0f;
            if (pushing != _wasPushing) { _wasPushing = pushing; if (pushing) Log.Dev("문 연출: 덜컹덜컹 (고양이가 민다)"); } // 2인 검증용

            if (!IsServer || Open.Value || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.25f;
            var pusher = CatWantsThrough();
            if (pusher == null)
            {
                if (_pushed > 0f) { _pushed = 0f; PushProgress.Value = 0f; }
                return;
            }
            _pushed += 0.25f;
            pusher.ServerHoldAtDoor(0.25f); // 미는 동안 잊지 않게
            PushProgress.Value = Mathf.Clamp01(_pushed / _balance.DoorPushSeconds);
            if (_pushed < _balance.DoorPushSeconds) return;
            Open.Value = true;
            _pushed = 0f;
            PushProgress.Value = 0f;
            Log.Dev("문: 고양이가 밀어서 열었다");
        }

        // 쫓거나 조사하는 고양이가 문 앞에 있나 (순찰 고양이는 안 민다 — design/cat-ideas/09)
        private CatBrain CatWantsThrough()
        {
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                if (cat == null || !cat.IsSpawned) continue;
                var st = cat.State.Value;
                if (st != CatState.Chase && st != CatState.Suspicious && st != CatState.Track && st != CatState.Search) continue;
                Vector3 d = cat.transform.position - transform.position; d.y = 0f;
                if (d.magnitude <= _balance.DoorPushRange) return cat;
            }
            return null;
        }

        private bool CatInDoorway(float range)
        {
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                if (cat == null) continue;
                Vector3 d = cat.transform.position - transform.position; d.y = 0f;
                if (d.magnitude <= range) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance, Transform hinge, NavMeshObstacle obstacle, Vector3 roomCenter, Vector3 roomSize)
        { _balance = balance; _hinge = hinge; _obstacle = obstacle; _roomCenter = roomCenter; _roomSize = roomSize; }
#endif
    }
}
