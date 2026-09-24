using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 방 모듈 접합점 (docs/10, 2026-09-24 고양이 57). 문 중앙에 두고 +Z가 방 바깥을 향한다.
    /// 다음 방은 자기 Entry의 +Z가 이 소켓의 -Z를 보게 돌린 뒤 위치를 맞춘다.
    /// </summary>
    public class DoorSocket : MonoBehaviour
    {
        [SerializeField] private float _width = 1.8f; // 문틈 폭 (고양이 에이전트가 지나가게 1.8 — 고양이 40)

        public float Width => _width;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(_width, 2f, 0.1f));
        }
    }
}
