using RatGame.Core;
using RatGame.Data;
using RatGame.Run;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 새 루프 목적지 상점 판매대 (docs/09, 2026-09-24 고양이 65). E로 열면 그 클라에만 StageShopPanel(EventBus — 규칙 3).
    /// 구매 판정·차감·택배 목록은 호스트만. 패널은 지갑·택배 목록 NV만 읽는다. 산 물건은 다음 맵 출발방에 놓인다.
    /// </summary>
    public class StageShopCounter : NetworkBehaviour, IInteractable
    {
        [SerializeField] private StageShopSO _shop;

        public NetworkVariable<int> Wallet = new(0);
        public NetworkVariable<FixedString128Bytes> PendingText = new(default);
        public NetworkVariable<bool> Closed = new(false); // 마지막 스테이지 — 살 수 없음

        public StageShopSO Shop => _shop;
        public string PromptText => Loc.T("상점");
        public float HoldSeconds => 0f;
        public bool CanInteract(ulong clientId) => true;

        public override void OnNetworkDespawn() => EventBus.RaiseWorldPanelSourceGone(this);

        // 개발 빌드 클라 전용 `-autoshop`: 쓸 식량이 생기면 첫 물건을 한 번 산다 — 사람 없이 2인 구매 경로(클라 → 호스트) 확인용
        public override void OnNetworkSpawn()
        {
            if (!IsServer && Debug.isDebugBuild && System.Environment.CommandLine.Contains("-autoshop")) InvokeRepeating(nameof(DevAutoBuy), 1f, 0.5f);
        }

        private void DevAutoBuy()
        {
            if (_shop == null || _shop.Entries.Length == 0 || Wallet.Value < _shop.Entries[0].Price) return;
            CancelInvoke(nameof(DevAutoBuy));
            Log.Dev($"개발: 자동 구매 요청 — {_shop.Entries[0].DisplayName} (지갑 {Wallet.Value})");
            RequestPurchase(0);
        }

        private void Update()
        {
            if (!IsServer) return;
            var run = RunManager.Instance;
            var quota = run != null ? run.GetComponent<StageQuota>() : null;
            if (quota == null) return;
            int wallet = quota.Wallet(run.StashedValue.Value);
            if (Wallet.Value != wallet) Wallet.Value = wallet;
            bool closed = quota.StageNumber.Value >= quota.StagesPerRun.Value;
            if (Closed.Value != closed) Closed.Value = closed;
            var text = new FixedString128Bytes(PendingSummary());
            if (!PendingText.Value.Equals(text)) PendingText.Value = text;
        }

        private string PendingSummary()
        {
            if (RunSession.PendingItems.Count == 0) return "";
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var id in RunSession.PendingItems) counts[id] = (counts.TryGetValue(id, out int c) ? c : 0) + 1;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var kv in counts) parts.Add(_shop.TryGet(kv.Key, out var e) ? Loc.Pack("{0} ×{1}", e.DisplayName, kv.Value) : kv.Key);
            // 클라가 자기 언어로 푼다 (Loc.Unpack). FixedString128 — UTF-8 한글 3바이트라 넘치기 전에 목록을 끊는다
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (System.Text.Encoding.UTF8.GetByteCount(sb.ToString() + part) > 120) break;
                if (sb.Length > 0) sb.Append(Loc.RecordSep);
                sb.Append(part);
            }
            return sb.ToString();
        }

        public void ServerInteract(ulong clientId) =>
            OpenClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });

        [ClientRpc]
        private void OpenClientRpc(ClientRpcParams rpcParams = default) => EventBus.RaiseWorldPanelRequested(WorldPanelKind.StageShop, this);

        /// <summary>패널 → 호스트: index 물건 하나 사기.</summary>
        public void RequestPurchase(int index) => PurchaseServerRpc(index);

        // 판매대는 호스트 소유 — 누구나 요청 (자판기와 같은 방식)
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PurchaseServerRpc(int index, RpcParams rpcParams = default)
        {
            ulong buyer = rpcParams.Receive.SenderClientId;
            var reply = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { buyer } } };
            var run = RunManager.Instance;
            var quota = run != null ? run.GetComponent<StageQuota>() : null;
            if (quota == null || _shop == null || index < 0 || index >= _shop.Entries.Length) { ResultClientRpc(false, Loc.Pack("살 수 없어요"), reply); return; }
            if (run.Phase.Value != RunPhase.StageActive) { ResultClientRpc(false, Loc.Pack("지금은 살 수 없어요"), reply); return; }
            var entry = _shop.Entries[index];
            if (!quota.ServerTrySpend(run, entry.Price, out string reason)) { ResultClientRpc(false, reason, reply); return; }
            quota.ServerCountBuy(); // 엔딩 요약 (고양이 108)
            RunSession.PendingItems.Add(entry.Id);
            Log.Dev($"상점: client {buyer}가 {entry.DisplayName}({entry.Price}) — 남은 식량 {RunSession.Pantry}, 창고 {run.StashedValue.Value}, 택배 {RunSession.PendingItems.Count}");
            ResultClientRpc(true, Loc.Pack("{0} 샀어요 — 다음 맵 출발방에", entry.DisplayName), reply);
        }

        [ClientRpc]
        private void ResultClientRpc(bool ok, string message, ClientRpcParams rpcParams = default)
        {
            message = Loc.Unpack(message); // 호스트가 묶어 보낸 틀·값을 내 언어로 (고양이 202)
            Log.Dev($"상점 결과 연출: {(ok ? "성공" : "실패")} — {message}"); // 2인 검증용
            EventBus.RaiseShopPurchaseResult(ok, message);
        }

#if UNITY_EDITOR
        public void EditorSetup(StageShopSO shop) => _shop = shop;
#endif
    }
}
