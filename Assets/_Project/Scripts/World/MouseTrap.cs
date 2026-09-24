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
    /// </summary>
    public class MouseTrap : TrapBase
    {
        [SerializeField] private Transform _bar; // 격발하면 탁 내려가는 막대

        public NetworkVariable<bool> Armed = new NetworkVariable<bool>(true);

        private Quaternion _barArmed;

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
