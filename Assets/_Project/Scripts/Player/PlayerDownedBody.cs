using RatGame.Core;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 다운 ↔ 대리 몸 (docs/04·05·09, 2026-09-24 고양이 46). Downed가 되면 호스트가 World/DownedBody를 띄우고,
    /// 모든 클라에서 본인 쥐의 콜라이더·렌더러를 끈다(끌리는 건 대리 몸). 소유 클라 시점은 0.1s마다 몸 위치로 옮겨 준다(docs/03 예외 — Nudge와 같은 RPC).
    /// 부활(Downed → 다른 상태)하면 몸 자리에 쥐를 세우고 몸을 없앤다.
    /// </summary>
    [RequireComponent(typeof(PlayerCondition))]
    public class PlayerDownedBody : NetworkBehaviour
    {
        [SerializeField] private GameObject _bodyPrefab;

        private PlayerCondition _condition;
        private Rigidbody _rb;
        private NetworkObject _body;
        private float _nextFollow;

        /// <summary>호스트: 지금 대리 몸 (테스트용).</summary>
        public NetworkObject Body => _body;

        private void Awake()
        {
            _condition = GetComponent<PlayerCondition>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            _condition.State.OnValueChanged += OnStateChanged;
            Apply(_condition.State.Value == ConditionState.Downed);
        }

        public override void OnNetworkDespawn()
        {
            _condition.State.OnValueChanged -= OnStateChanged;
            if (IsServer && _body != null && _body.IsSpawned) _body.Despawn();
        }

        private void OnStateChanged(ConditionState prev, ConditionState next)
        {
            bool downed = next == ConditionState.Downed;
            Apply(downed);
            if (!IsServer) return;
            if (downed) SpawnBody();
            else if (prev == ConditionState.Downed) ReleaseBody();
        }

        // 모든 클라: 쓰러진 쥐 본체는 숨기고 부딪히지 않게 (대리 몸이 대신 보이고 끌린다)
        private void Apply(bool downed)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = !downed;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !downed;
            if (IsOwner && _rb != null) _rb.isKinematic = downed;
        }

        private void SpawnBody()
        {
            if (_bodyPrefab == null || (_body != null && _body.IsSpawned)) return;
            var go = Instantiate(_bodyPrefab, transform.position + Vector3.down * 0.3f, Quaternion.Euler(0f, transform.eulerAngles.y, 90f));
            _body = go.GetComponent<NetworkObject>();
            _body.Spawn(true);
            go.GetComponent<DownedBody>().Owner.Value = OwnerClientId;
            Log.Dev($"다운 몸: client {OwnerClientId} 몸 생성 @ {go.transform.position:F1}");
        }

        private void ReleaseBody()
        {
            if (_body == null) return;
            Vector3 stand = _body.transform.position + Vector3.up * 0.7f;
            Teleport(stand);
            if (_body.IsSpawned) _body.Despawn();
            _body = null;
            Log.Dev($"다운 몸: client {OwnerClientId} 일어남 @ {stand:F1}");
        }

        private void Update()
        {
            if (!IsServer || _body == null || !_body.IsSpawned || Time.time < _nextFollow) return;
            _nextFollow = Time.time + 0.1f;
            Teleport(_body.transform.position + Vector3.up * 0.4f); // 시점이 끌려가는 몸을 따라간다
        }

        private void Teleport(Vector3 pos)
        {
            GetComponent<PlayerController>()?.TeleportClientRpc(pos, transform.rotation, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject bodyPrefab) => _bodyPrefab = bodyPrefab;
#endif
    }
}
