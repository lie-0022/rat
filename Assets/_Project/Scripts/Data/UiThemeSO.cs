using TMPro;
using UnityEngine;

namespace RatGame.Data
{
    /// <summary>UI 색 역할 — 값이 아니라 쓰임새로 지정한다 (docs/12 공통 스타일). 직렬화가 정수라 새 역할은 끝에만 추가.</summary>
    public enum UiColorRole
    {
        None, Dim, Panel, Row, Secondary, Accent, OnAccent, AccentText, Text, TextMuted, Positive, Danger,
        Warning, PositiveText, DangerText
    }

    /// <summary>UI 글자 크기 단계 (1920×1080 기준).</summary>
    public enum UiTextRole { None, Title, Heading, Body, Small }

    /// <summary>
    /// UI 공통 스타일 (docs/12). 색·글자 크기·글꼴을 한 곳에 모은다 — 정적 요소에는 ThemedGraphic으로 역할만 붙이고,
    /// 런타임에 색을 바꾸는 위젯은 이 에셋을 직접 참조해 역할로 꺼내 쓴다. 바꾼 뒤 Tools/RatGame/UI/Apply Theme.
    /// 톤: 쥐구멍·창고 느낌의 따뜻한 갈색 패널 + 노랑 강조.
    /// </summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "RatGame/UI Theme")]
    public class UiThemeSO : ScriptableObject
    {
        [Header("글꼴")]
        [SerializeField] private TMP_FontAsset _font;

        [Header("색 (역할)")]
        [SerializeField] private Color _dim = new(0f, 0f, 0f, 0.55f);             // 화면 가림막·HUD 박스
        [SerializeField] private Color _panel = new(0.12f, 0.10f, 0.08f, 1f);     // 창 배경 (불투명 — 뒤 월드 라벨이 비치지 않게)
        [SerializeField] private Color _row = new(0f, 0f, 0f, 0.35f);             // 목록 행·스크롤 영역
        [SerializeField] private Color _secondary = new(0.35f, 0.30f, 0.25f, 1f); // 보조 버튼 (닫기)·해금된 행
        [SerializeField] private Color _accent = new(0.95f, 0.75f, 0.25f, 1f);    // 주 버튼 (구매·착용)·선택 슬롯
        [SerializeField] private Color _onAccent = new(0.15f, 0.10f, 0f, 1f);     // 주 버튼 위 글자
        [SerializeField] private Color _accentText = new(1f, 0.9f, 0.5f, 1f);     // 제목·레벨·가치
        [SerializeField] private Color _text = Color.white;
        [SerializeField] private Color _textMuted = new(0.8f, 0.8f, 0.8f, 1f);    // 설명·보조 문구
        [SerializeField] private Color _positive = new(0.2f, 0.6f, 0.25f, 0.85f); // 적립 +N·귀환 배경
        [SerializeField] private Color _danger = new(0.8f, 0.15f, 0.1f, 0.9f);    // 전멸·지침 배경
        [SerializeField] private Color _warning = new(1f, 0.7f, 0.2f, 1f);        // 스태미나 낮음·던지기 차지
        [SerializeField] private Color _positiveText = new(0.75f, 1f, 0.75f, 1f); // 성공 문구
        [SerializeField] private Color _dangerText = new(1f, 0.6f, 0.6f, 1f);     // 실패 문구

        [Header("글자 크기")]
        [SerializeField] private float _title = 36f;
        [SerializeField] private float _heading = 28f;
        [SerializeField] private float _body = 24f;
        [SerializeField] private float _small = 18f;

        public TMP_FontAsset Font => _font;

        public Color GetColor(UiColorRole role) => role switch
        {
            UiColorRole.Dim => _dim,
            UiColorRole.Panel => _panel,
            UiColorRole.Row => _row,
            UiColorRole.Secondary => _secondary,
            UiColorRole.Accent => _accent,
            UiColorRole.OnAccent => _onAccent,
            UiColorRole.AccentText => _accentText,
            UiColorRole.Text => _text,
            UiColorRole.TextMuted => _textMuted,
            UiColorRole.Positive => _positive,
            UiColorRole.Danger => _danger,
            UiColorRole.Warning => _warning,
            UiColorRole.PositiveText => _positiveText,
            UiColorRole.DangerText => _dangerText,
            _ => Color.white
        };

        public float GetSize(UiTextRole role) => role switch
        {
            UiTextRole.Title => _title,
            UiTextRole.Heading => _heading,
            UiTextRole.Body => _body,
            UiTextRole.Small => _small,
            _ => _body
        };

#if UNITY_EDITOR
        public void EditorSetFont(TMP_FontAsset font) => _font = font;
#endif
    }
}
