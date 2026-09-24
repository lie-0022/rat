using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>기지 거울 = 스킨 선택 (docs/11). E로 열면 그 클라에만 MirrorPanel. 착용 자체는 개인(PlayerSkin 소유 클라).</summary>
    public class Mirror : NetworkBehaviour, IInteractable
    {


        public string PromptText => "거울";
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
            EventBus.RaiseWorldPanelRequested(WorldPanelKind.Mirror, this);
        }
    }
}
