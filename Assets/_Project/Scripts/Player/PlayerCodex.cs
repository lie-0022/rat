using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 도감 해금 (docs/08·11). 호스트가 정산 이벤트를 받아 각 플레이어의 소유 클라에 Id를 보내고, 각자 자기 저장에 넣는다.
    /// 조회는 소유 클라 로컬 (SaveService).
    /// </summary>
    public class PlayerCodex : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (IsServer) EventBus.LootDeposited += OnLootDeposited;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) EventBus.LootDeposited -= OnLootDeposited;
        }

        private void OnLootDeposited(LootItemSO item, int value)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return;
            UnlockClientRpc(item.Id, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        private void UnlockClientRpc(string itemId, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            var ids = SaveService.Data.UnlockedCodexIds;
            if (ids.Contains(itemId)) return;
            ids.Add(itemId);
            SaveService.Save();
            Log.Dev($"도감 해금: {itemId} ({ids.Count}종)");
        }

        public bool IsUnlocked(string itemId) => SaveService.Data.UnlockedCodexIds.Contains(itemId);
    }
}
