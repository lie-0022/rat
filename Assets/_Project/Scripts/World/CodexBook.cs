using RatGame.Data;
using RatGame.UI;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>기지 도감 책 (docs/11). E로 열면 그 클라에만 CodexPanel — 해금 목록은 각자 저장에서 읽는다.</summary>
    public class CodexBook : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private CodexPanel _panelPrefab;

        private CodexPanel _panel;

        public string PromptText => "도감";
        public float HoldSeconds => 0f;
        public bool CanInteract(ulong clientId) => true;

        public override void OnNetworkDespawn()
        {
            if (_panel != null) Destroy(_panel.gameObject);
        }

        public void ServerInteract(ulong clientId)
        {
            OpenClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }

        [ClientRpc]
        private void OpenClientRpc(ClientRpcParams rpcParams = default)
        {
            if (_panel == null) _panel = Instantiate(_panelPrefab);
            _panel.Open(_database);
        }
    }
}
