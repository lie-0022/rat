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
    /// 아래 팔레트(2026-09-24): 견본을 누르면 바로 입고, 색상·채도·밝기 슬라이더로 자유 색을 만들어 "이 색 입기". 다른 쥐와 겹쳐도 된다.
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
        [SerializeField] private TMP_Text _hexText;
        [SerializeField] private UnityEngine.UI.Button _applyButton;

        private PlayerSkin _skin;
        private readonly List<MirrorRow> _rows = new();
        private bool _open;
        private float _openedAt;

        public bool IsOpen => _open;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _closeButton.onClick.AddListener(() => Close(false));
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
                _applyButton.onClick.AddListener(() => { if (_skin != null) _skin.SetCustomColor(SliderColor); });
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

        private void RefreshPreview()
        {
            if (_preview == null) return;
            var color = SliderColor;
            _preview.color = color;
            if (_hexText != null) _hexText.text = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private void PickSwatch(Color color)
        {
            SetSliders(color);
            if (_skin != null) _skin.SetCustomColor(color);
        }

        private void OnDestroy()
        {
            if (_open) InputFocus.PanelClosed(false);
        }

        public void Open()
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() : null;
            var skin = player != null ? player.GetComponent<PlayerSkin>() : null;
            if (skin == null) return;
            if (_skin != skin) { _skin = skin; BuildRows(); }
            if (_open) return;
            _open = true;
            _openedAt = Time.unscaledTime;
            SetSliders(_skin.CurrentColor);
            _root.SetActive(true);
            InputFocus.PanelOpened();
        }

        public void Close(bool byEscape = false)
        {
            if (!_open) return;
            _open = false;
            _root.SetActive(false);
            InputFocus.PanelClosed(byEscape);
        }

        private void Update()
        {
            if (!_open || _skin == null) return;
            if (UiCommon.ClosePressed(_openedAt, out bool byEscape)) { Close(byEscape); return; }
            string current = _skin.HasCustomColor ? "#palette" : _skin.CurrentId; // 팔레트 색이면 스킨 줄은 전부 "착용"
            foreach (var row in _rows) row.Refresh(current);
        }

#if UNITY_EDITOR
        public void EditorSetupPalette(Transform swatchParent, UnityEngine.UI.Slider hue, UnityEngine.UI.Slider saturation, UnityEngine.UI.Slider value,
                                       UnityEngine.UI.Image preview, TMP_Text hexText, UnityEngine.UI.Button apply)
        {
            _swatchParent = swatchParent; _hue = hue; _saturation = saturation; _value = value; _preview = preview; _hexText = hexText; _applyButton = apply;
        }
#endif

        private void BuildRows()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
            // 첫 줄: 기본색 (스킨 없음)
            var basic = Instantiate(_rowPrefab, _rowsParent);
            basic.Bind("", "기본 (팀 색)", _skin.GetComponent<PlayerVisual>().DefaultColor, _skin.Equip);
            _rows.Add(basic);
            foreach (var so in _skin.Skins)
            {
                if (so == null) continue;
                var row = Instantiate(_rowPrefab, _rowsParent);
                row.Bind(so.Id, so.DisplayName, so.TintColor, _skin.Equip);
                _rows.Add(row);
            }
        }
    }
}
