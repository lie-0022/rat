using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 프리팹에 박힌 고정 문구(버튼·제목·항목 이름)를 언어에 맞게 바꿔 그린다 (docs/12).
    /// 한국어 원문은 에디터 도구가 붙일 때 저장해 둔다 — 켜질 때 글자에서 읽으면, 이미 영어로 바뀐 틀을 복제한 줄이
    /// 영어를 원문으로 잡아 한국어로 못 돌아온다. 코드가 매번 새로 쓰는 글자에는 붙이지 않는다(그건 Loc.T).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField, TextArea] private string _ko;

        private TMP_Text _text;

        public string Ko { get => _ko; set => _ko = value; }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            if (string.IsNullOrEmpty(_ko)) _ko = _text.text;
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
