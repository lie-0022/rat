using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 고양이 방울 물건 (상점, 고양이 140). 던진 방울이 날아가거나 구르는 동안 고양이 `catBellHitMeters` 안(수평)을 지나면
    /// 그 고양이 목에 달리고(CatBrain.Belled) 물건은 사라진다. 빗나가면 바닥에 남아 다시 주워 던질 수 있다. 판정은 호스트.
    /// </summary>
    [RequireComponent(typeof(CarryableItem))]
    public class CatBell : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        private CarryableItem _carryable;
        private bool _used;

        private void Awake() => _carryable = GetComponent<CarryableItem>();

        // 던진 뒤 1.5초(IsRecentlyThrown) 동안 매 물리 스텝 — 첫 착지 한 번만 보면 굴러서 고양이 옆에 멈춰도 못 잡았다(시험에서 0.7m 옆인데 안 달림)
        private void FixedUpdate()
        {
            if (!IsServer || _used || !_carryable.IsRecentlyThrown || _carryable.CarrierIds.Count > 0) return;
            CatBrain best = null; float bestD = _balance.CatBellHitMeters;
            Vector3 p = transform.position;
            foreach (var c in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                Vector3 d = c.transform.position - p;
                if (Mathf.Abs(d.y) > 1.5f) continue; // 선반 위·아래 고양이는 빼고
                d.y = 0f;
                if (d.magnitude <= bestD) { bestD = d.magnitude; best = c; }
            }
            if (best == null || !best.ServerAttachBell()) return;
            _used = true;
            Log.Dev($"방울: client {_carryable.AttributedClient} → {best.name} ({bestD:F2}m)");
            NetworkObject.DespawnSafe();
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
