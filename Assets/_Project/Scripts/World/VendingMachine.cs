using RatGame.Core;
using RatGame.Data;
using RatGame.Meta;
using RatGame.Player;
using RatGame.Run;
using RatGame.UI;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 기지 자판기 = 업그레이드 상점 (docs/11). E로 열면 그 클라에만 ShopPanel을 띄운다.
    /// 구매 판정·누계 차감·저장은 호스트만 (규칙 1). 패널은 NetworkVariable(누계·레벨)만 읽는다.
    /// </summary>
    public class VendingMachine : NetworkBehaviour, IInteractable
    {
        [SerializeField] private UpgradeSO[] _upgrades;
        [SerializeField] private ShopPanel _panelPrefab;

        public NetworkVariable<int> HaulTotal = new(0);
        public NetworkVariable<UpgradeLevels> Levels = new();

        private ShopPanel _panel;

        public UpgradeSO[] Upgrades => _upgrades;
        public string PromptText => "상점";
        public float HoldSeconds => 0f;
        public bool CanInteract(ulong clientId) => true;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            HaulTotal.Value = RunSession.TotalValue;
            Levels.Value = MetaUpgrades.Levels;
        }

        public override void OnNetworkDespawn()
        {
            if (_panel != null) Destroy(_panel.gameObject);
        }

        private void Update()
        {
            if (!IsServer) return;
            // 귀환 정산·구매로 바뀐 저장값을 그대로 비춘다 (값이 같으면 NetworkVariable은 안 보냄)
            if (HaulTotal.Value != RunSession.TotalValue) HaulTotal.Value = RunSession.TotalValue;
            var levels = MetaUpgrades.Levels;
            if (!Levels.Value.Equals(levels)) Levels.Value = levels;
        }

        public void ServerInteract(ulong clientId)
        {
            OpenShopClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });
        }

        [ClientRpc]
        private void OpenShopClientRpc(ClientRpcParams rpcParams = default)
        {
            if (_panel == null) _panel = Instantiate(_panelPrefab);
            _panel.Open(this);
        }

        /// <summary>패널 → 호스트: index 항목 한 레벨 구매.</summary>
        public void RequestPurchase(int index) => PurchaseServerRpc(index);

        // 자판기는 호스트 소유 — 누구나 요청할 수 있어야 한다 (NGO 2.x Rpc API, RequireOwnership 대체)
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PurchaseServerRpc(int index, RpcParams rpcParams = default)
        {
            ulong buyer = rpcParams.Receive.SenderClientId;
            var reply = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { buyer } } };
            if (index < 0 || index >= _upgrades.Length) { PurchaseResultClientRpc(false, "없는 항목", reply); return; }

            var so = _upgrades[index];
            int level = MetaUpgrades.GetLevel(so.Effect);
            if (level >= so.MaxLevel) { PurchaseResultClientRpc(false, "최대 레벨", reply); return; }

            int price = so.PriceForLevel(level + 1);
            if (SaveService.Data.HaulTotal < price) { PurchaseResultClientRpc(false, $"누계 부족 ({price} 필요)", reply); return; }

            SaveService.Data.HaulTotal -= price;
            MetaUpgrades.SetLevel(so.Effect, level + 1); // 누계 차감까지 같이 저장
            PlayerUpgrades.ServerApplyToAll(MetaUpgrades.Levels);
            Log.Dev($"구매: client {buyer} {so.DisplayName} Lv{level + 1} (-{price}, 누계 {SaveService.Data.HaulTotal})");
            PurchaseResultClientRpc(true, $"{so.DisplayName} Lv{level + 1}", reply);
        }

        [ClientRpc]
        private void PurchaseResultClientRpc(bool ok, string message, ClientRpcParams rpcParams = default)
        {
            if (_panel != null) _panel.ShowMessage(ok, message);
        }
    }
}
