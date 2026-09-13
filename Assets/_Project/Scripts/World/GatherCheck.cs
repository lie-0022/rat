using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// "다운 안 된 전원이 이 바닥 위에 모였나" 판정 (호스트 전용, docs/09·11).
    /// 쥐구멍 귀환과 기지 출발 발판이 같은 규칙을 쓴다.
    /// </summary>
    public static class GatherCheck
    {
        public static void Count(BoxCollider area, out int ready, out int needed)
        {
            ready = 0;
            needed = 0;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var condition = client.PlayerObject.GetComponent<PlayerCondition>();
                if (condition != null && condition.State.Value == ConditionState.Downed) continue; // 쓰러진 사람은 두고 간다
                needed++;
                if (area != null && ContainsFootprint(area, client.PlayerObject.transform.position)) ready++;
            }
        }

        /// <summary>위치가 바닥 영역 위인지. 트리거가 바닥에 얇게 깔려 있어 수평 범위만 엄격히 보고 높이는 여유를 둔다.</summary>
        public static bool ContainsFootprint(BoxCollider box, Vector3 worldPos)
        {
            var bounds = box.bounds;
            if (worldPos.y < bounds.min.y - 0.5f || worldPos.y > bounds.max.y + 2f) return false;
            Vector3 local = box.transform.InverseTransformPoint(worldPos) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }
    }
}
