using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 먹기 (docs/04 "치즈류 전리품을 '먹기'(Interact 길게)로 즉시 50 회복 — 아이템 소멸(가치 포기). 갈등 설계", docs/05 Edible — 2026-09-24 고양이 48).
    /// 손에 든 Edible 소형(주머니·대형 제외)을 E 1s 길게 → 호스트가 물건을 없애고 먹은 쥐의 클라에 스태미나 회복을 보낸다(스태미나는 소유 클라 값).
    /// </summary>
    [RequireComponent(typeof(CarryableItem))]
    public class EdibleItem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;

        private CarryableItem _item;

        public string PromptText => "먹기 (스태미나 회복)";
        public float HoldSeconds => _balance != null ? _balance.EatSeconds : 1f;

        private void Awake() => _item = GetComponent<CarryableItem>();

        // 내 손에 든 것만 — 바닥·남의 손·주머니는 안 된다
        public bool CanInteract(ulong clientId) =>
            _item != null && _item.IsSpawned && !_item.IsHeavy && !_item.Pocketed.Value && _item.CarrierIds.Contains(clientId);

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer || !CanInteract(clientId)) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return;
            var carry = client.PlayerObject.GetComponent<PlayerCarryController>();
            if (carry == null || !carry.ServerConsumeCarried(_item)) return;
            float amount = _balance.EatStamina;
            AteClientRpc(amount, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            Log.Dev($"먹기: client {clientId}가 {name} 먹음 (+{amount} 스태미나, 가치 {_item.EffectiveValue} 포기)");
            NetworkObject.Despawn();
        }

        [ClientRpc]
        private void AteClientRpc(float amount, ClientRpcParams rpc = default)
        {
            var po = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            var stamina = po != null ? po.GetComponent<PlayerStamina>() : null;
            if (stamina != null) stamina.Restore(amount);
            EventBus.RaiseCheeseEaten(amount); // 토스트 (규칙 3)
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
