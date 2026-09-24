using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 쥐 전용 배관 (docs/06, 고양이 87). 안에서 뛰면 쇠관이 울려 **양쪽 입구 밖**에서 쿵쿵 소리가 난다 —
    /// 관 속 소리는 벽에 막혀 새지 않아서, 소리가 관을 타고 끝으로 나오는 것으로 친다. PlayerNoiseEmitter가 천장 레이로 찾는다.
    /// 배관 루트 = 두 문 가운데, forward = 관 방향, localScale.z = 길이 (GridZoneLayout).
    /// </summary>
    public class PipeEcho : MonoBehaviour
    {
        private const float OutsideMouth = 0.4f; // 입구 판 바깥(방 쪽) — 소리가 판에 또 막히지 않게

        /// <summary>이 자리(쥐 캡슐 가운데)가 배관 안인가 — 발 위 짧은 거리에 배관 천장. 방은 천장이 없어서 선반 밑 말고는 배관뿐, 표시 컴포넌트로 확정.</summary>
        public static PipeEcho Above(Vector3 pos)
        {
            return Physics.Raycast(pos, Vector3.up, out var hit, 1.2f, ~LayerMask.GetMask("Player", "Ragdoll"), QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<PipeEcho>() : null;
        }

        public void Mouths(out Vector3 a, out Vector3 b)
        {
            Vector3 f = transform.forward; f.y = 0f; f.Normalize();
            float half = transform.localScale.z * 0.5f + OutsideMouth;
            a = transform.position - f * half;
            b = transform.position + f * half;
        }
    }
}
