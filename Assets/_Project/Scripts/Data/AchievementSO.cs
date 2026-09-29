using UnityEngine;

namespace RatGame.Data
{
    /// <summary>도전과제 (docs/11). Title·Desc는 한국어 원문 — 표시할 때 Loc.T. 에셋은 Resources/Achievements.</summary>
    [CreateAssetMenu(menuName = "RatGame/Achievement", fileName = "ach_")]
    public class AchievementSO : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Desc;
        public string StatKey;
        public int Target = 1;
        /// <summary>보상 스킨 — 지금은 표시만, 잠그지 않는다 (판단 부탁 14 전).</summary>
        public SkinSO RewardSkin;
    }
}
