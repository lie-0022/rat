using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 도감 해금 (docs/08·11). 호스트가 정산 이벤트를 받아 각 플레이어의 소유 클라에 Id를 보내고, 각자 자기 저장에 넣는다.
    /// 조회는 소유 클라 로컬 (SaveService). 이번 런에 새로 채운 도감은 결과 화면 칩용으로 런 시드와 함께 기억한다.
    /// </summary>
    public class PlayerCodex : NetworkBehaviour
    {
        private readonly List<string> _newThisRun = new();
        private int _newThisRunSeed;

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
            // 런 밖(기지 연습 등)은 시드 0 — 결과 화면에 안 잡힌다
            int runSeed = Run.RunManager.Instance != null ? Run.RunManager.Instance.RunSeed.Value : 0;
            UnlockClientRpc(item.Id, runSeed, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        private void UnlockClientRpc(string itemId, int runSeed, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            var ids = SaveService.Data.UnlockedCodexIds;
            if (ids.Contains(itemId)) return;
            ids.Add(itemId);
            SaveService.Save();
            if (_newThisRunSeed != runSeed) { _newThisRun.Clear(); _newThisRunSeed = runSeed; }
            _newThisRun.Add(itemId);
            EventBus.RaiseCodexUnlocked(itemId); // 토스트 등 로컬 구독자용 (소유 클라에서만 발행)
            EventBus.RaiseAchievementStat("codexCount", ids.Count, true); // 수집가 (고양이 220)
            Log.Dev($"도감 해금: {itemId} ({ids.Count}종)");
        }

        public bool IsUnlocked(string itemId) => SaveService.Data.UnlockedCodexIds.Contains(itemId);

        /// <summary>해당 런(시드)에서 새로 채운 도감 Id — 다른 런이면 빈 목록.</summary>
        public IReadOnlyList<string> NewUnlocksFor(int runSeed) =>
            runSeed != 0 && runSeed == _newThisRunSeed ? _newThisRun : System.Array.Empty<string>();
    }
}
