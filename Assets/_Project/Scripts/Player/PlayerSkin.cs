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

        /// <summary>팔레트 털 색 (거울). 알파 0 = 안 고름 → 스킨 또는 팀 기본색. 겹쳐도 된다(2026-09-24 사용자 결정).</summary>
        public NetworkVariable<Color32> CustomColor = new(new Color32(0, 0, 0, 0),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public SkinSO[] Skins => _skins;
        public bool HasCustomColor => CustomColor.Value.a > 0;
        public string CurrentId => SkinId.Value.ToString();

        public override void OnNetworkSpawn()
        {
            SkinId.OnValueChanged += OnSkinChanged;
            CustomColor.OnValueChanged += OnCustomColorChanged;
            if (IsOwner)
            {
                SkinId.Value = SaveService.Data.EquippedSkinId ?? "";
                CustomColor.Value = ColorUtility.TryParseHtmlString(SaveService.Data.BodyColorHex, out var saved)
                    ? (Color32)saved : new Color32(0, 0, 0, 0);
            }
            Apply(CurrentId);
        }

        public override void OnNetworkDespawn()
        {
            SkinId.OnValueChanged -= OnSkinChanged;
            CustomColor.OnValueChanged -= OnCustomColorChanged;
        }

        /// <summary>소유 클라: 착용 + 저장. id가 비면 기본색. 팔레트 색은 지운다(나중에 고른 게 이긴다).</summary>
        public void Equip(string id)
        {
            if (!IsOwner) return;
            SkinId.Value = id ?? "";
            CustomColor.Value = new Color32(0, 0, 0, 0);
            SaveService.Data.EquippedSkinId = id ?? "";
            SaveService.Data.BodyColorHex = "";
            SaveService.Save();
            Log.Dev($"스킨 착용: {(string.IsNullOrEmpty(id) ? "기본" : id)}");
        }

        /// <summary>소유 클라: 팔레트 색 입기 + 저장. 스킨은 벗는다.</summary>
        public void SetCustomColor(Color color)
        {
            if (!IsOwner) return;
            color.a = 1f;
            SkinId.Value = "";
            CustomColor.Value = color;
            SaveService.Data.EquippedSkinId = "";
            SaveService.Data.BodyColorHex = "#" + ColorUtility.ToHtmlStringRGB(color);
            SaveService.Save();
            Log.Dev($"털 색: {SaveService.Data.BodyColorHex}");
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

        private void OnCustomColorChanged(Color32 prev, Color32 now)
        {
            Apply(CurrentId);
            Log.Dev($"털 색 연출: client {OwnerClientId} → {(now.a == 0 ? "없음" : "#" + ColorUtility.ToHtmlStringRGB(now))}"); // 2인 검증용
        }

        // 우선순위: 스킨 > 팔레트 색 > 팀 기본색 (둘 다 저장되는 일은 없다 — 고를 때 서로 지움)
        private void Apply(string id)
        {
            var visual = GetComponent<PlayerVisual>();
            if (visual == null) return;
            var skin = Find(id);
            if (skin != null) visual.SetBodyColor(skin.TintColor);
            else if (HasCustomColor) visual.SetBodyColor(CustomColor.Value);
            else visual.ResetBodyColor();
        }

        /// <summary>입고 있는 색으로 다시 칠한다 — 거울 미리보기(로컬로만 칠함)를 취소할 때.</summary>
        public void ReapplyColor() => Apply(CurrentId);

        /// <summary>지금 입고 있는 털 색 (거울 미리보기용).</summary>
        public Color CurrentColor
        {
            get
            {
                var skin = Find(CurrentId);
                if (skin != null) return skin.TintColor;
                if (HasCustomColor) return CustomColor.Value;
                var visual = GetComponent<PlayerVisual>();
                return visual != null ? visual.DefaultColor : Color.gray;
            }
        }
    }
}
