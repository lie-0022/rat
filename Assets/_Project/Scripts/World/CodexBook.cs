using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>기지 도감 책 (docs/11). E로 열면 그 클라에만 CodexPanel — 해금 목록은 각자 저장에서 읽는다.</summary>
    public class CodexBook : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDatabase _database;


        /// <summary>도감 패널이 읽는 전체 목록.</summary>
        public ItemDatabase Database => _database;

        public string PromptText => "도감";
        public float HoldSeconds => 0f;
        public bool CanInteract(ulong clientId) => true;

        public override void OnNetworkDespawn()
        {
            EventBus.RaiseWorldPanelSourceGone(this);
        }

        public void ServerInteract(ulong clientId)
        {
            OpenClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }

        [ClientRpc]
        private void OpenClientRpc(ClientRpcParams rpcParams = default)
        {
            EventBus.RaiseWorldPanelRequested(WorldPanelKind.Codex, this);
        }
    }
}
