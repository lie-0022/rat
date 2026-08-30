using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 클라별 몸 색 (그레이박스 구분용 — 진짜 스킨 시스템은 태스크 2-5).
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

        public override void OnNetworkSpawn()
        {
            var color = Palette[(int)(OwnerClientId % (ulong)Palette.Length)];
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            // 몸통(루트 렌더러)만 — 귀·코·꼬리는 핑크 유지
            var body = GetComponent<MeshRenderer>();
            if (body != null) body.SetPropertyBlock(block);
        }
    }
}
