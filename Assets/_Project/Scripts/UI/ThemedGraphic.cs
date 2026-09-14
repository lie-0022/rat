using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// UI 요소의 색·글자 크기·글꼴을 UiThemeSO의 역할에서 가져온다 (docs/12 공통 스타일).
    /// 스크립트가 런타임에 바꾸는 색(선택 강조·잠김·카운트다운 등)에는 붙이지 않는다 — 덮어쓰기 때문.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemedGraphic : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private UiColorRole _color = UiColorRole.None;
        [SerializeField] private UiTextRole _text = UiTextRole.None;

        public void Setup(UiThemeSO theme, UiColorRole color, UiTextRole text)
        {
            _theme = theme;
            _color = color;
            _text = text;
            Apply();
        }

        private void Awake() => Apply();

#if UNITY_EDITOR
        // OnValidate 안에서 다른 컴포넌트를 바로 건드리면 경고가 나서 한 틱 미룬다
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif

        public void Apply()
        {
            if (_theme == null) return;
            var graphic = GetComponent<UnityEngine.UI.Graphic>();
            if (graphic == null) return;
            if (_color != UiColorRole.None) graphic.color = _theme.GetColor(_color);
            if (_text != UiTextRole.None && graphic is TMP_Text tmp)
            {
                tmp.fontSize = _theme.GetSize(_text);
                if (_theme.Font != null) tmp.font = _theme.Font;
            }
        }
    }
}
