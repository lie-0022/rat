using Unity.Netcode;

namespace RatGame.Core
{
    public static class NetworkObjectExtensions
    {
        /// <summary>
        /// 호스트: 디스폰 — 씬에 놓인 오브젝트(창고 데모의 전리품 등)는 파괴하지 않는다. Despawn(true)는 NGO가 "in-scene 파괴" 경고를 찍어서(고양이 134).
        /// 화면에서 끄는 건 각자 OnNetworkDespawn에서 (CarryableItem).
        /// </summary>
        public static void DespawnSafe(this NetworkObject no)
        {
            if (no != null && no.IsSpawned) no.Despawn(no.IsSceneObject != true);
        }
    }
}
