using RatGame.Core;
using RatGame.Noise;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 전기선 (docs/10 Trap_WireShock — 2026-09-24 고양이 49). 켜짐·꺼짐을 번갈아 반복 — 켜진 동안 닿으면 Stunned + 찌직 소리.
    /// 꺼진 틈을 보고 지나가는 타이밍 퍼즐. 켜짐 연출은 On NV로 전 클라.
    /// </summary>
    public class WireShock : TrapBase
    {
        [SerializeField] private Renderer _wire;

        public NetworkVariable<bool> On = new NetworkVariable<bool>(false);

        private float _nextToggle;
        private MaterialPropertyBlock _block;

        public override void OnNetworkSpawn()
        {
            _block = new MaterialPropertyBlock();
            On.OnValueChanged += (_, on) => ApplyWire();
            ApplyWire();
        }

        private void ApplyWire()
        {
            if (_wire == null) return;
            _block.SetColor("_BaseColor", On.Value ? new Color(0.6f, 0.9f, 1f) : new Color(0.15f, 0.15f, 0.2f));
            _wire.SetPropertyBlock(_block);
        }

        protected override void Update()
        {
            if (IsServer && Time.time >= _nextToggle)
            {
                On.Value = !On.Value;
                _nextToggle = Time.time + (On.Value ? _balance.WireOnSeconds : _balance.WireOffSeconds);
            }
            base.Update();
        }

        protected override void OnRat(PlayerCondition rat)
        {
            if (!On.Value) return;
            rat.ServerStun(_balance.WireStunSeconds);
            NoiseSystem.Emit(transform.position, _balance.WireZapLoudness, NoiseType.Trap, rat.OwnerClientId);
            Log.Dev($"전기선: client {rat.OwnerClientId} 감전 — 기절 {_balance.WireStunSeconds}s");
        }

#if UNITY_EDITOR
        public void EditorSetupWire(Renderer wire) => _wire = wire;
#endif
    }
}
