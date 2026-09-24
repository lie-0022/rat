using UnityEngine;

namespace RatGame.AI
{
    /// <summary>스팟 종류 (design/cat-design/02 카탈로그). 직렬화가 정수라 새 종류는 끝에만 추가.</summary>
    public enum CatSpotType { Look, Bed, Food, Sun, Groom, Water, Litter, Perch, Ambush, Box, Door /* 집주인이 부르면 나가고 들어오는 문 (2026-09-24) — 순찰 대상 아님 */ }

    /// <summary>
    /// 고양이가 "볼일 보러" 가는 지점 (웨이포인트 대체 — 좌표 + 종류 + 머무는 행동). 방 모듈·씬에 배치.
    /// 종류별 머무는 시간·감각 배율은 BalanceConfigSO, 여기선 지점별 가중치와 바라볼 방향(transform.forward)만.
    /// </summary>
    public class CatSpot : MonoBehaviour
    {
        [SerializeField] private CatSpotType _type = CatSpotType.Look;
        [SerializeField, Min(0.1f)] private float _weight = 1f;   // 다음 목적지로 뽑힐 상대 확률

        public CatSpotType Type => _type;
        public float Weight => _weight;

#if UNITY_EDITOR
        public void EditorSetup(CatSpotType type, float weight) { _type = type; _weight = weight; }

        private void OnDrawGizmos()
        {
            Gizmos.color = _type switch
            {
                CatSpotType.Bed => new Color(0.6f, 0.4f, 1f),
                CatSpotType.Food => new Color(1f, 0.6f, 0.2f),
                CatSpotType.Sun => new Color(1f, 0.9f, 0.3f),
                CatSpotType.Groom => new Color(0.4f, 0.9f, 0.9f),
                _ => new Color(0.8f, 0.8f, 0.8f)
            };
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, $"{_type}");
        }
#endif
    }
}
