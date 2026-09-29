using RatGame.Core;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>결과 화면 "오늘의 쥐들" 한 줄 — 색 견본·이름·쥐구멍 적립·들고 온 것·일꾼 왕관·다운 표시.</summary>
    public class ResultPlayerRow : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _swatch;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _depositText;
        [SerializeField] private TMP_Text _carriedText;
        [SerializeField] private GameObject _crown;
        [SerializeField] private GameObject _downedTag;

        public void Show(string displayName, Color color, int deposited, int depositCount, int carried,
                         bool crown, bool downed, bool returned, string depositLabel = "쥐구멍")
        {
            if (_swatch.color != color) _swatch.color = color;
            SetText(_nameText, displayName);
            SetText(_depositText, depositCount > 0 ? Loc.F("{0} {1} ({2}개)", depositLabel, deposited, depositCount) : $"{depositLabel} —");
            // 전멸이면 들고 온 것은 의미 없음 — 다운 표시만
            bool showCarried = returned && carried > 0;
            SetText(_carriedText, showCarried ? Loc.F("들고 옴 {0}", carried) : "");
            // 들고 온 줄이 없으면 적립 글자를 줄 가운데로 (위 절반에 떠 보이지 않게)
            var depositRect = _depositText.rectTransform;
            float bottom = showCarried ? 0.5f : 0f;
            if (!Mathf.Approximately(depositRect.anchorMin.y, bottom))
            {
                depositRect.anchorMin = new Vector2(depositRect.anchorMin.x, bottom);
                depositRect.offsetMin = new Vector2(depositRect.offsetMin.x, 0f);
                _depositText.alignment = showCarried ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.MidlineRight;
            }
            if (_crown.activeSelf != crown) _crown.SetActive(crown);
            if (_downedTag.activeSelf != downed) _downedTag.SetActive(downed);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
