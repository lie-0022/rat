using UnityEngine;

namespace RatGame.Data
{
    /// <summary>스킨 (docs/11) — 모델 1개에 틴트 + 모자. 도전과제로 해금 (AchievementId 비면 기본 해금).</summary>
    [CreateAssetMenu(fileName = "Skin_", menuName = "RatGame/Skin")]
    public class SkinSO : ScriptableObject
    {
        [SerializeField] private string _id;           // "skin_yellow"
        [SerializeField] private string _displayName;  // "노란 쥐"
        [SerializeField] private Color _tintColor = Color.white;
        [SerializeField] private GameObject _hatPrefab; // 머리 소켓 부착 (아트 단계, null 가능)
        [SerializeField] private string _achievementId; // 해금 조건 (도전과제 미구현 — 지금은 전부 해금)

        public string Id => _id;
        public string DisplayName => _displayName;
        public Color TintColor => _tintColor;
        public GameObject HatPrefab => _hatPrefab;
        public string AchievementId => _achievementId;
    }
}
