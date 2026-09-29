using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 프리팹에 박힌 고정 문구(버튼·제목·항목 이름)를 언어에 맞게 바꿔 그린다 (docs/12).
    /// 한국어 원문은 처음 켜질 때 글자에서 읽어 둔다 — 코드가 매번 새로 쓰는 글자에는 붙이지 않는다(그건 Loc.T).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        private TMP_Text _text;
        private string _ko;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _ko = _text.text;
        }

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Loc.Changed -= Apply;

        private void Apply()
        {
            string s = Loc.T(_ko);
            if (_text.text != s) _text.text = s;
        }
    }
}
