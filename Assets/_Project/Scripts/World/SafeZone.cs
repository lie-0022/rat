using System.Collections.Generic;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 안전지대 (팀 대화 "최종 목적지가 안전 지대" — docs/09 새 루프, 2026-09-25 고양이 72). 목적지방 안.
    /// 이 박스 안의 쥐는 고양이가 보지도 듣지도 못하고, 쫓던 쥐가 들어오면 고양이가 포기한다. 고양이 NavMesh도 빠진다(방 프리팹의 NavMeshModifierVolume).
    /// 방이 네트워크로 스폰돼 호스트·클라 모두에 있지만 판정은 호스트(고양이)만 쓴다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SafeZone : MonoBehaviour
    {
        private static readonly List<SafeZone> _all = new();
        private BoxCollider _box;

        private void Awake() { _box = GetComponent<BoxCollider>(); _box.isTrigger = true; }
        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        /// <summary>안전지대 안인가 (높이 무관).</summary>
        public static bool Contains(Vector3 pos)
        {
            foreach (var z in _all)
            {
                Vector3 local = z.transform.InverseTransformPoint(pos) - z._box.center;
                Vector3 half = z._box.size * 0.5f;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z) return true;
            }
            return false;
        }
    }
}
