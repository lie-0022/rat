using RatGame.Core;
using RatGame.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 착용 스킨 (docs/11). 개인 저장(EquippedSkinId) → 소유 클라가 NetworkVariable에 쓰고 전 클라가 틴트 적용.
    /// 빈 Id = 스킨 없음 → PlayerVisual의 클라별 기본 팔레트.
    /// </summary>
    public class PlayerSkin : NetworkBehaviour
    {
        [SerializeField] private SkinSO[] _skins;

        public NetworkVariable<FixedString32Bytes> SkinId = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public SkinSO[] Skins => _skins;
        public string CurrentId => SkinId.Value.ToString();

        public override void OnNetworkSpawn()
        {
            SkinId.OnValueChanged += OnSkinChanged;
            if (IsOwner) SkinId.Value = SaveService.Data.EquippedSkinId ?? "";
            Apply(CurrentId);
        }

        public override void OnNetworkDespawn() => SkinId.OnValueChanged -= OnSkinChanged;

        /// <summary>소유 클라: 착용 + 저장. id가 비면 기본색.</summary>
        public void Equip(string id)
        {
            if (!IsOwner) return;
            SkinId.Value = id ?? "";
            SaveService.Data.EquippedSkinId = id ?? "";
            SaveService.Save();
            Log.Dev($"스킨 착용: {(string.IsNullOrEmpty(id) ? "기본" : id)}");
        }

        public SkinSO Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var s in _skins) if (s != null && s.Id == id) return s;
            return null;
        }

        private void OnSkinChanged(FixedString32Bytes prev, FixedString32Bytes now)
        {
            Apply(now.ToString());
            Log.Dev($"스킨 연출: client {OwnerClientId} → {(now.Length == 0 ? "기본" : now.ToString())}"); // 2인 검증용 (다른 쥐 화면에 반영)
        }

        private void Apply(string id)
        {
            var visual = GetComponent<PlayerVisual>();
            if (visual == null) return;
            var skin = Find(id);
            if (skin != null) visual.SetBodyColor(skin.TintColor);
            else visual.ResetBodyColor();
        }
    }
}
