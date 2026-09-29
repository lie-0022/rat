using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쥐가 숨는 곳 — 신발·상자·커튼 뒤 (design/cat-ideas/14). E로 들어가고 E로 나온다(호스트 판정).
    /// 안에 있으면 ConditionState.Hidden: 이동·잡기 불가, 고양이 시야 판정에서 빠진다. 나올 때 소음.
    /// 고양이는 수색(CatState.Search) 중 이 스팟을 킁킁거리고, 건드리면 안의 쥐를 끄집어낸다(ServerEject).
    /// 대형을 끌고 있으면 못 들어간다 — 놓고 숨을지 들고 뛸지(필러 1).
    /// </summary>
    public class HideSpot : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private string _displayName = "신발";
        [SerializeField, Min(1)] private int _capacity = 1;
        [SerializeField] private Transform _insideAnchor;   // 숨은 쥐가 서 있을 자리 (없으면 자기 위치)
        [SerializeField] private Transform _exitAnchor;     // 나올 자리 (없으면 앞 0.7m)

        // 호스트 전용 — 누가 안에 있나
        private readonly List<ulong> _occupants = new();

        /// <summary>안에 있는 쥐 수 (HUD·수색용, 복제).</summary>
        public NetworkVariable<int> OccupantCount = new(0);
        /// <summary>고양이가 입구에 앉아 있다 — 드나들 수 없다 (design/cat-ideas/09).</summary>
        public NetworkVariable<bool> CatBlocking = new(false);

        public string DisplayName => _displayName;
        public int Capacity => _capacity;
        // 앵커가 없으면 박스 크기로 계산 (그레이박스 = 단위 큐브 스케일): 안 = 바닥 중앙, 출구 = 앞면 0.5m 밖 바닥
        public Vector3 InsidePosition => _insideAnchor != null ? _insideAnchor.position : FloorPoint(transform.position);
        public Vector3 ExitPosition => _exitAnchor != null ? _exitAnchor.position
            : FloorPoint(transform.position + transform.forward * (Mathf.Abs(transform.lossyScale.z) * 0.5f + 0.5f));

        private Vector3 FloorPoint(Vector3 p) { p.y = transform.position.y - Mathf.Abs(transform.lossyScale.y) * 0.5f + RatHalfHeight; return p; }
        private const float RatHalfHeight = 0.65f; // 쥐 캡슐(높이 2 × 스케일 0.6) 절반 + 여유 — 피벗이 몸 중앙

        public string PromptText => Loc.F(IsOccupant(LocalClientIdSafe()) ? "{0}에서 나오기" : "{0}에 숨기", Loc.T(_displayName));
        // 들어가기는 짧은 홀드(급하게), 나오기는 즉시
        public float HoldSeconds => IsOccupant(LocalClientIdSafe()) ? 0f : (_balance != null ? _balance.HideEnterSeconds : 0.3f);

        public bool CanInteract(ulong clientId)
        {
            if (CatBlocking.Value) return false; // 고양이가 입구를 막았다 — 안에 있으면 갇힘
            if (IsOccupant(clientId)) return true;
            if (OccupantCount.Value >= _capacity) return false;
            // 대형을 끌고는 못 숨는다 — 클라 프롬프트용 근사(호스트에서 다시 검사)
            var player = FindPlayer(clientId);
            var carry = player != null ? player.GetComponent<PlayerCarryController>() : null;
            return carry == null || !carry.IsDraggingHeavy;
        }

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer) return;
            if (IsOccupant(clientId)) { ServerExit(clientId, byCat: false); return; }
            if (_occupants.Count >= _capacity) return;
            var player = FindPlayer(clientId);
            if (player == null) return;
            var condition = player.GetComponent<PlayerCondition>();
            var carry = player.GetComponent<PlayerCarryController>();
            if (condition == null || condition.State.Value != ConditionState.Active) return;
            if (carry != null && carry.IsDraggingHeavy) return;

            _occupants.Add(clientId);
            OccupantCount.Value = _occupants.Count;
            condition.ServerSetState(ConditionState.Hidden);
            Teleport(player, InsidePosition);
            Log.Dev($"숨기: client {clientId} → {_displayName} ({_occupants.Count}/{_capacity})");
        }

        /// <summary>고양이가 건드려 안의 쥐를 전부 끄집어낸다 (수색 발각). 나온 쥐 목록 반환.</summary>
        public List<PlayerCondition> ServerEject()
        {
            var ejected = new List<PlayerCondition>();
            if (!IsServer) return ejected;
            foreach (ulong id in _occupants.ToArray())
            {
                var player = FindPlayer(id);
                var condition = player != null ? player.GetComponent<PlayerCondition>() : null;
                ServerExit(id, byCat: true);
                if (condition != null) ejected.Add(condition);
            }
            return ejected;
        }

        private void ServerExit(ulong clientId, bool byCat)
        {
            _occupants.Remove(clientId);
            OccupantCount.Value = _occupants.Count;
            var player = FindPlayer(clientId);
            if (player == null) return;
            var condition = player.GetComponent<PlayerCondition>();
            if (condition != null && condition.State.Value == ConditionState.Hidden) condition.ServerSetState(ConditionState.Active);
            Teleport(player, ExitPosition);
            // 나올 때 소음 — 고양이가 아직 근처면 다시 의심 (docs/06 채널)
            NoiseSystem.Emit(ExitPosition, _balance != null ? _balance.HideExitNoise : 35f, NoiseType.Impact, clientId);
            Log.Dev($"{(byCat ? "발각" : "나오기")}: client {clientId} ← {_displayName}");
        }

        public override void OnNetworkDespawn()
        {
            // 씬 전환 등으로 사라지면 안의 쥐를 풀어 준다 (Hidden에 갇히지 않게)
            if (IsServer) foreach (ulong id in _occupants.ToArray()) ServerExit(id, byCat: false);
        }

        private bool IsOccupant(ulong clientId) => _occupants.Contains(clientId) ||
            (!IsServer && OccupantCount.Value > 0 && IsLocalHiddenNear(clientId)); // 클라: 자기 상태로 근사

        // 클라에는 occupant 목록이 없다 — 내가 Hidden이고 이 스팟 1m 안이면 여기 숨은 것
        private bool IsLocalHiddenNear(ulong clientId)
        {
            var player = FindPlayer(clientId);
            var condition = player != null ? player.GetComponent<PlayerCondition>() : null;
            return condition != null && condition.State.Value == ConditionState.Hidden
                   && Vector3.Distance(player.transform.position, InsidePosition) < 1f;
        }

        private static ulong LocalClientIdSafe() => NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;

        private static GameObject FindPlayer(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            var obj = nm.SpawnManager != null ? nm.SpawnManager.GetPlayerNetworkObject(clientId) : null;
            return obj != null ? obj.gameObject : null;
        }

        private static void Teleport(GameObject player, Vector3 pos)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
                pc.TeleportClientRpc(pos, player.transform.rotation, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { pc.OwnerClientId } }
                });
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance, string name, int capacity)
        {
            _balance = balance; _displayName = name; _capacity = capacity;
        }
#endif
    }
}
