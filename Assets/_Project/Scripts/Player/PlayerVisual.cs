using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 몸 색. 기본은 클라별 팔레트(그레이박스 구분용), 스킨(PlayerSkin)이 있으면 그 틴트.
    /// MaterialPropertyBlock이라 머티리얼 에셋은 오염 안 됨.
    /// </summary>
    public class PlayerVisual : NetworkBehaviour
    {
        private static readonly Color[] Palette =
        {
            new(0.80f, 0.80f, 0.84f), // P1 회색 쥐
            new(0.55f, 0.70f, 0.95f), // P2 파랑 쥐
            new(0.60f, 0.88f, 0.55f), // P3 초록 쥐
            new(0.95f, 0.85f, 0.45f), // P4 노랑 쥐
        };

        // HUD 이름 — 팀 상태·토스트·결과 화면이 같은 이름을 쓴다 (Steam 이름 연결 전까지)
        private static readonly string[] PaletteNames = { "회색 쥐", "파랑 쥐", "초록 쥐", "노랑 쥐" };

        public override void OnNetworkSpawn() => ResetBodyColor();

        public Color DefaultColor => ColorFor(OwnerClientId);

        /// <summary>클라 Id의 팀 기본색 — 나간 플레이어도 Id만으로 표시할 수 있게 정적.</summary>
        public static Color ColorFor(ulong clientId) => Palette[(int)(clientId % (ulong)Palette.Length)];
        public static string ColorNameFor(ulong clientId) => PaletteNames[(int)(clientId % (ulong)PaletteNames.Length)];

        public void ResetBodyColor() => SetBodyColor(DefaultColor);

        public void SetBodyColor(Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            // 몸통(루트 렌더러)만 — 귀·코·꼬리는 핑크 유지
            var body = GetComponent<MeshRenderer>();
            if (body != null) body.SetPropertyBlock(block);
        }
    }
}
