using RatGame.Core;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쓰러진 동료의 몸 (docs/04·05·09 "다운된 몸은 mass 3의 Carryable — 동료가 쥐구멍까지 운반", 2026-09-24 고양이 46).
    /// 플레이어 몸은 소유 클라 권한이라 호스트 물리(조인트)로 끌 수 없다 → 호스트 소유의 대리 몸을 띄우고 동료는 이걸 대형처럼 끈다.
    /// 쥐구멍에 닿으면 DepositZone이 정산 대신 부활시킨다. 본인 쥐는 숨겨지고 시점만 이 몸을 따라간다(PlayerDownedBody).
    /// </summary>
    [RequireComponent(typeof(CarryableItem))]
    public class DownedBody : NetworkBehaviour
    {
        public NetworkVariable<ulong> Owner = new NetworkVariable<ulong>(ulong.MaxValue);

        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        private void Awake() { _renderer = GetComponentInChildren<Renderer>(); _block = new MaterialPropertyBlock(); }

        public override void OnNetworkSpawn()
        {
            Owner.OnValueChanged += (_, id) => ApplyColor(id);
            ApplyColor(Owner.Value);
        }

        private void ApplyColor(ulong id)
        {
            if (id == ulong.MaxValue || _renderer == null) return;
            // 주인 털빛을 칙칙하게 — 누가 쓰러졌는지 보인다
            _block.SetColor("_BaseColor", Color.Lerp(PlayerVisual.ColorFor(id), Color.gray, 0.4f));
            _renderer.SetPropertyBlock(_block);
            Log.Dev($"다운 몸 연출: client {id}"); // 2인 검증용
        }
    }
}
