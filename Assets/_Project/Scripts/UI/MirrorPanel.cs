using System.Collections.Generic;
using RatGame.Core;
using RatGame.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 거울 = 스킨 선택 패널 (uGUI, 로컬). 내 PlayerSkin의 목록을 행으로 만들고, 고르면 PlayerSkin.Equip.
    /// 해금은 도전과제 미구현이라 전부 열림 (docs/11).
    /// 아래 팔레트(2026-09-24): 견본을 누르면 바로 입고, 색상·채도·밝기 슬라이더나 색 코드 입력으로 자유 색을 만들어 "이 색 입기". 다른 쥐와 겹쳐도 된다.
    /// 만드는 동안은 내 쥐만 로컬로 칠해 거울에 비친 모습으로 미리 본다(저장·동기화 없음). 입지 않고 닫으면 원래 색으로.
    /// </summary>
    public class MirrorPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private MirrorRow _rowPrefab;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [Header("팔레트")]
        [SerializeField] private Transform _swatchParent;          // 견본 버튼들 (버튼 Image 색 = 그 견본 색)
        [SerializeField] private UnityEngine.UI.Slider _hue;
        [SerializeField] private UnityEngine.UI.Slider _saturation;
        [SerializeField] private UnityEngine.UI.Slider _value;
        [SerializeField] private UnityEngine.UI.Image _preview;
        [SerializeField] private TMP_InputField _hexInput;         // "#RRGGBB" 직접 입력
        [SerializeField] private UnityEngine.UI.Button _applyButton;
        [SerializeField] private TMP_Text _previewHint;             // 미리보기 중 안내 (입어야 저장)

        private PlayerVisual _visual;
        private bool _previewing;                                   // 로컬로만 칠한 상태 — 닫으면 되돌린다
        private bool _typingLastFrame;

        private PlayerSkin _skin;
        private readonly List<MirrorRow> _rows = new();
        private bool _open;
        private float _openedAt;

        public bool IsOpen => _open;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _closeButton.onClick.AddListener(() => Close(false));
            Loc.Changed += OnLanguageChanged;
            if (_swatchParent != null)
                foreach (var button in _swatchParent.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    var color = button.GetComponent<UnityEngine.UI.Image>().color;
                    button.onClick.AddListener(() => PickSwatch(color));
                }
            if (_hue != null)
            {
                _hue.onValueChanged.AddListener(_ => RefreshPreview());
                _saturation.onValueChanged.AddListener(_ => RefreshPreview());
                _value.onValueChanged.AddListener(_ => RefreshPreview());
                _applyButton.onClick.AddListener(() => { if (_skin != null) { _skin.SetCustomColor(SliderColor); _previewing = false; RefreshHint(); } });
            }
            if (_hexInput != null)
            {
                _hexInput.onValueChanged.AddListener(OnHexTyped);
                _hexInput.onEndEdit.AddListener(_ => WriteHex(SliderColor)); // 잘못 친 채로 나가면 지금 색 코드로 되돌림
            }
            _root.SetActive(false);
        }

        private Color SliderColor => Color.HSVToRGB(_hue.value, _saturation.value, _value.value);

        // 슬라이더를 이 색에 맞춘다 (알림 없이 — 끌 때만 미리보기가 바뀌게)
        private void SetSliders(Color color)
        {
            if (_hue == null) return;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            _hue.SetValueWithoutNotify(h);
            _saturation.SetValueWithoutNotify(s);
            _value.SetValueWithoutNotify(v);
            RefreshPreview();
        }

        // 슬라이더가 움직였다(사람 손) — 미리보기 + 내 쥐 로컬 칠하기
        private void RefreshPreview()
        {
            if (_preview == null) return;
            var color = SliderColor;
            _preview.color = color;
            if (_hexInput == null || !_hexInput.isFocused) WriteHex(color); // 치는 중엔 덮어쓰지 않는다
            PreviewOnBody(color);
        }

        private void PreviewOnBody(Color color)
        {
            if (_visual == null || !_open) return;
            _visual.SetBodyColor(color);
            _previewing = !Approximately(color, _skin.CurrentColor);
            RefreshHint();
        }

        private void WriteHex(Color color)
        {
            if (_hexInput == null) return;
            _hexInput.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(color));
        }

        // 색 코드: "#A8ED4C"·"a8ed4c" 둘 다. 6자리가 되면 바로 반영
        private void OnHexTyped(string text)
        {
            string hex = text.Trim().TrimStart('#');
            if (hex.Length != 6 || !ColorUtility.TryParseHtmlString("#" + hex, out var color)) return;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            _hue.SetValueWithoutNotify(h);
            _saturation.SetValueWithoutNotify(s);
            _value.SetValueWithoutNotify(v);
            _preview.color = color;
            PreviewOnBody(color);
        }

        private void RefreshHint()
        {
            if (_previewHint == null) return;
            string text = _previewing ? Loc.T("미리보기 중 — [이 색 입기]를 눌러야 저장돼요") : "";
            if (_previewHint.text != text) _previewHint.text = text;
        }

        private static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        private void PickSwatch(Color color)
        {
            if (_skin != null) _skin.SetCustomColor(color);
            SetSliders(color);
            _previewing = false;
            RefreshHint();
        }

        private void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            if (_open) InputFocus.PanelClosed(false);
        }

        // 줄은 처음 열 때 한 번 만든다 — 언어를 바꾸면 이름·설명이 옛 언어로 남아서 다시 만든다 (고양이 315)
        private void OnLanguageChanged() { if (_skin != null) BuildRows(); }

        public void Open()
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() : null;
            var skin = player != null ? player.GetComponent<PlayerSkin>() : null;
            if (skin == null) return;
            if (_skin != skin) { _skin = skin; BuildRows(); }
            if (_open) return;
            _open = true;
            _openedAt = Time.unscaledTime;
            _visual = _skin.GetComponent<PlayerVisual>();
            SetSliders(_skin.CurrentColor);
            _previewing = false;
            RefreshHint();
            _root.SetActive(true);
            InputFocus.PanelOpened();
        }

        public void Close(bool byEscape = false)
        {
            if (!_open) return;
            _open = false;
            if (_previewing && _skin != null) _skin.ReapplyColor(); // 입지 않은 미리보기는 되돌린다
            _previewing = false;
            _root.SetActive(false);
            InputFocus.PanelClosed(byEscape);
        }

        private void Update()
        {
            if (!_open || _skin == null) return;
            // 색 코드를 치는 중엔 E(16진수 글자)로 닫히지 않게 — Esc는 입력칸이 먼저 먹고 한 번 더 누르면 닫힌다
            // 입력칸이 같은 프레임에 Esc를 먹고 포커스를 놓을 수 있어서, 직전 프레임까지 치고 있었어도 닫지 않는다
            bool focused = _hexInput != null && _hexInput.isFocused;
            bool typing = focused || _typingLastFrame;
            _typingLastFrame = focused;
            if (!typing && UiCommon.ClosePressed(_openedAt, out bool byEscape)) { Close(byEscape); return; }
            string current = _skin.HasCustomColor ? "#palette" : _skin.CurrentId; // 팔레트 색이면 스킨 줄은 전부 "착용"
            foreach (var row in _rows) row.Refresh(current);
        }

#if UNITY_EDITOR
        public void EditorSetupPalette(Transform swatchParent, UnityEngine.UI.Slider hue, UnityEngine.UI.Slider saturation, UnityEngine.UI.Slider value,
                                       UnityEngine.UI.Image preview, TMP_InputField hexInput, UnityEngine.UI.Button apply, TMP_Text previewHint)
        {
            _swatchParent = swatchParent; _hue = hue; _saturation = saturation; _value = value; _preview = preview;
            _hexInput = hexInput; _applyButton = apply; _previewHint = previewHint;
        }
#endif

        private void BuildRows()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
            // 첫 줄: 기본색 (스킨 없음)
            var basic = Instantiate(_rowPrefab, _rowsParent);
            basic.Bind("", Loc.T("기본 (팀 색)"), _skin.GetComponent<PlayerVisual>().DefaultColor, _skin.Equip);
            _rows.Add(basic);
            foreach (var so in _skin.Skins)
            {
                if (so == null) continue;
                var row = Instantiate(_rowPrefab, _rowsParent);
                row.Bind(so.Id, Loc.T(so.DisplayName), so.TintColor, _skin.Equip);
                _rows.Add(row);
            }
        }
    }
}
