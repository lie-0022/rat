using RatGame.Core;
using RatGame.Noise;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쥐덫 (docs/10 Trap_MouseTrap, docs/06 "쥐덫 격발 80" — 2026-09-24 고양이 49). 밟으면 Downed + 소음 80, 1회성.
    /// 물건을 던져 넣거나 떨어뜨리면 헛격발 — 쥐는 무사하지만 소리는 똑같이 크다(고양이가 온다).
    /// 미끼(고양이 129): 판 위에 작은 음식을 얹어 둘 수 있다. 웅크린 채 집으면 살금살금 성공, 서서 집으면 탁 — 기절 + 떨어뜨림 + 소음.
    /// 미끼가 밀려 떨어져도 탁(다친 쥐 없음). 판정은 호스트, 결과 안내는 집은 쥐에게만.
    /// </summary>
    public class MouseTrap : TrapBase
    {
        [SerializeField] private Transform _bar; // 격발하면 탁 내려가는 막대

        public NetworkVariable<bool> Armed = new NetworkVariable<bool>(true);

        private Quaternion _barArmed;
        private CarryableItem _bait;

        protected override void Awake()
        {
            base.Awake();
            if (_bar != null) _barArmed = _bar.localRotation;
        }

        public override void OnNetworkSpawn()
        {
            Armed.OnValueChanged += (_, armed) => { ApplyBar(); if (!armed) Log.Dev("쥐덫 연출: 탁!"); }; // 2인 검증용
            ApplyBar();
        }

        private void ApplyBar()
        {
            if (_bar != null) _bar.localRotation = Armed.Value ? _barArmed : _barArmed * Quaternion.Euler(0f, 0f, -170f);
        }

        protected override bool WantsItems => Armed.Value;

        /// <summary>호스트: 판 위에 미끼를 얹는다 (GridZoneBuilder, 고양이 129).</summary>
        public void ServerSetBait(CarryableItem item)
        {
            if (!IsServer || item == null || !Armed.Value) return;
            Vector3 top = transform.position + Vector3.up * 0.12f;
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = top; }
            item.transform.position = top;
            _bait = item;
        }

        public bool HasBait => _bait != null;

        protected override void Update()
        {
            base.Update();
            CheckBaitHint();
            if (!IsServer || _bait == null) return;
            if (!Armed.Value || !_bait.IsSpawned) { _bait = null; return; }
            if (_bait.CarrierIds.Count > 0) { OnBaitTaken(_bait.CarrierIds[0]); return; }
            // 판 밖으로 밀려나면 탁 — 물건을 던지거나 밀어 미끼를 떨어뜨리는 게 쥐덫을 치우는 방법
            if (!Contains(_bait.transform.position, 0.6f))
            {
                _bait = null;
                Snap(null);
                Log.Dev("쥐덫 미끼: 밀려 떨어짐 — 탁 (헛격발)");
            }
        }

        private void OnBaitTaken(ulong clientId)
        {
            _bait = null;
            var po = NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) ? client.PlayerObject : null;
            bool sneaky = po != null && po.transform.localScale.y < PlayerController.BaseScaleY * 0.75f; // 웅크림 = 스케일 (PlayerScent와 같은 판정)
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
            if (sneaky)
            {
                Log.Dev($"쥐덫 미끼: client {clientId} 살금살금 성공");
                BaitResultClientRpc(true, target);
                return;
            }
            Snap(clientId);
            if (po != null)
            {
                po.GetComponent<PlayerCarryController>()?.ServerForceDrop(); // 앞발이 찍혀 놓친다
                po.GetComponent<PlayerCondition>()?.ServerStun(_balance.TrapBaitStunSeconds);
            }
            Log.Dev($"쥐덫 미끼: client {clientId} 서서 집음 — 탁! 기절");
            BaitResultClientRpc(false, target);
        }

        // 미끼 규칙 안내 (고양이 132) — 미끼 얹힌 쥐덫에 처음 다가갔을 때 한 번(세션당). 스테이지 안내 토스트는 3칸이 차서 따로.
        // 미끼 여부는 호스트만 알지만 "판 위에 음식이 있다"는 모두에게 보이는 위치라 각자 계산 — 동기화 없음.
        private static bool _baitHintShown;
        private float _nextHintCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHint() => _baitHintShown = false;

        private void CheckBaitHint()
        {
            if (_baitHintShown || !Armed.Value || Time.time < _nextHintCheck) return;
            _nextHintCheck = Time.time + 0.5f;
            var me = NetworkManager != null && NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if (me == null || (me.transform.position - transform.position).sqrMagnitude > _balance.TrapBaitHintMeters * _balance.TrapBaitHintMeters) return;
            foreach (var item in FindObjectsByType<CarryableItem>(FindObjectsSortMode.None)) // 가까이 왔을 때만 — 드물다
            {
                if (item.IsHeavy || item.CarrierIds.Count > 0 || !Contains(item.transform.position, 0.6f)) continue;
                _baitHintShown = true;
                Log.Dev("쥐덫 미끼 안내 연출"); // 2인 검증용
                EventBus.RaiseTrapBaitNear();
                return;
            }
        }

        [ClientRpc]
        private void BaitResultClientRpc(bool sneaky, ClientRpcParams rpcParams = default)
        {
            Log.Dev($"쥐덫 미끼 연출: {(sneaky ? "살금살금" : "탁")}"); // 2인 검증용
            EventBus.RaiseTrapBait(sneaky);
        }

        protected override void OnRat(PlayerCondition rat)
        {
            if (!Armed.Value) return;
            Snap(rat.OwnerClientId);
            rat.ServerSetState(ConditionState.Downed);
            Log.Dev($"쥐덫: client {rat.OwnerClientId} 걸림 — 다운");
        }

        protected override void OnItem(CarryableItem item)
        {
            // 방금 놓이거나 던져진 물건만 — 원래 옆에 놓여 있던 물건으로는 안 터진다
            if (!Armed.Value || Time.time - item.LastReleaseTime > 1f) return;
            Snap(item.AttributedClient);
            Log.Dev($"쥐덫: {item.name}에 헛격발");
        }

        private void Snap(ulong? by)
        {
            Armed.Value = false;
            NoiseSystem.Emit(transform.position, _balance.TrapMouseLoudness, NoiseType.Trap, by);
        }

#if UNITY_EDITOR
        public void EditorSetupBar(Transform bar) => _bar = bar;
#endif
    }
}
