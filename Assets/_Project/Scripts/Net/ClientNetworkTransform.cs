using Unity.Netcode.Components;

namespace RatGame.Net
{
    /// <summary>
    /// 소유 클라 권한 NetworkTransform (NGO 샘플 패턴). 플레이어 이동 전용 —
    /// docs/03 결정: 반응성이 물리 코미디의 생명, 리컨실리에이션은 스코프 초과.
    /// 전리품·AI 등 다른 오브젝트에 붙이면 안 된다 (그쪽은 호스트 권한).
    /// </summary>
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
