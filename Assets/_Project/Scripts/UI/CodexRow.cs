using RatGame.Core;
using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>도감 한 줄 — 이름·가치·한 줄 설명. 해금은 Secondary 배경, 미해금은 "???"와 Row 배경 (테마 역할).</summary>
    public class CodexRow : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private UnityEngine.UI.Image _background;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private TMP_Text _flavorText;

        public void Set(LootItemSO item, bool unlocked)
        {
            if (_theme != null) _background.color = _theme.GetColor(unlocked ? UiColorRole.Secondary : UiColorRole.Row);
            _nameText.text = unlocked ? Loc.T(item.DisplayName) : "???";
            _valueText.text = unlocked ? $"{item.BaseValue}" : "-";
            _flavorText.text = unlocked ? Loc.T(item.CodexFlavor) : Loc.T("아직 못 가져온 물건");
        }

        /// <summary>도전과제 한 줄 (고양이 221) — 제목·진행도(또는 "완료")·설명(+보상). 달성은 도감 해금과 같은 배경.</summary>
        public void SetAchievement(AchievementSO a, bool done, int progress)
        {
            if (_theme != null) _background.color = _theme.GetColor(done ? UiColorRole.Secondary : UiColorRole.Row);
            _nameText.text = Loc.T(a.Title);
            _valueText.text = done ? Loc.T("완료") : $"{Mathf.Min(progress, a.Target)}/{a.Target}";
            string reward = a.RewardSkin != null ? Loc.F("  · 보상: {0}", Loc.T(a.RewardSkin.DisplayName)) : "";
            _flavorText.text = Loc.T(a.Desc) + reward;
        }
    }
}
