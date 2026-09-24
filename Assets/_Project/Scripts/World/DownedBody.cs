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

        [SerializeField] private Renderer _furRenderer;        // 쥐 모델 털 (없으면 첫 렌더러 — 예전 캡슐)
        [SerializeField] private int _furMaterialIndex = -1;   // 털 머티리얼 칸, -1 = 렌더러 전체

        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        private void Awake() { _renderer = _furRenderer != null ? _furRenderer : GetComponentInChildren<Renderer>(); _block = new MaterialPropertyBlock(); }

        public override void OnNetworkSpawn()
        {
            Owner.OnValueChanged += (_, id) => ApplyColor(id);
            ApplyColor(Owner.Value);
        }

        private void ApplyColor(ulong id)
        {
            if (id == ulong.MaxValue || _renderer == null) return;
            // 주인 털빛(팔레트·스킨 포함)을 칙칙하게 — 누가 쓰러졌는지 보인다
            var visual = FindVisual(id);
            Color fur = visual != null ? visual.CurrentBodyColor : PlayerVisual.ColorFor(id);
            if (fur.a <= 0f) fur = PlayerVisual.ColorFor(id);
            if (_furMaterialIndex >= 0) _renderer.GetPropertyBlock(_block, _furMaterialIndex); else _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", Color.Lerp(fur, Color.gray, 0.4f));
            if (_furMaterialIndex >= 0) _renderer.SetPropertyBlock(_block, _furMaterialIndex); else _renderer.SetPropertyBlock(_block);
            Log.Dev($"다운 몸 연출: client {id}"); // 2인 검증용
        }

        // GetPlayerNetworkObject는 클라에서 남의 쥐를 못 찾고 Netcode 오류를 찍는다(고양이 127) — 스폰 목록에서 직접
        private PlayerVisual FindVisual(ulong id)
        {
            if (NetworkManager.SpawnManager == null) return null;
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
                if (obj.IsPlayerObject && obj.OwnerClientId == id) return obj.GetComponent<PlayerVisual>();
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetupModel(Renderer fur, int index) { _furRenderer = fur; _furMaterialIndex = index; }
#endif
    }
}
