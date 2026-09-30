using System;
using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 설정 패널 (docs/12 설정, 메인 메뉴·일시정지 공용). 열 때 현재 설정의 사본을 고치고 [적용]으로 확정 — 닫으면 적용 안 한 변경은 버린다.
    /// 커서·입력 잠금은 여는 쪽(일시정지 창)이 맡는다 — 메인 메뉴에선 닫아도 커서를 잠그면 안 되므로.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private static readonly string[] CrouchOptions = { "누르고 있기", "눌러서 전환" };
        private static readonly string[] ShakeOptions = { "켬", "끔" }; // 큰 고양이 발걸음 등 (고양이 145)
        private static readonly string[] ScreenModeOptions = { "전체 화면", "테두리 없는 창", "창 모드" };
        private static readonly string[] LanguageOptions = { "한국어", "English" };
        private static readonly string[] LanguageCodes = { "ko", "en" };

        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private GameObject _root;
        [SerializeField] private SettingsSliderRow _sensitivity;
        [SerializeField] private SettingsStepper _crouch;
        [SerializeField] private SettingsStepper _cameraShake;
        [SerializeField] private SettingsStepper _soundCaptions; // 고양이 258
        [SerializeField] private SettingsStepper _screenMode;
        [SerializeField] private SettingsStepper _resolution;
        [SerializeField] private SettingsSliderRow _masterVolume;
        [SerializeField] private SettingsSliderRow _sfxVolume;
        [SerializeField] private SettingsSliderRow _musicVolume;
        [SerializeField] private SettingsSliderRow _voiceVolume;
        [SerializeField] private SettingsStepper _language;
        [SerializeField] private TMP_Text _languageNote;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private UnityEngine.UI.Button _defaultsButton;
        [SerializeField] private UnityEngine.UI.Button _applyButton;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private float _messageSeconds = 2.5f;

        private readonly List<Vector2Int> _resolutions = new();
        private SettingsData _draft;
        private bool _open;
        private float _openedAt;
        private float _messageUntil;
        private int _closedFrame = -1;

        public bool IsOpen => _open;
        /// <summary>닫힌 프레임 — 같은 Esc/E로 뒤의 일시정지 창까지 닫히지 않게.</summary>
        public bool ClosedThisFrame => _closedFrame == Time.frameCount;
        public event Action Closed;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _sensitivity.Changed += v => Edit(d => d.MouseSensitivity = v);
            _masterVolume.Changed += v => Edit(d => d.MasterVolume = v);
            _sfxVolume.Changed += v => Edit(d => d.SfxVolume = v);
            _musicVolume.Changed += v => Edit(d => d.MusicVolume = v);
            _voiceVolume.Changed += v => Edit(d => d.VoiceVolume = v);
            _crouch.Changed += i => Edit(d => d.CrouchToggle = i == 1);
            if (_cameraShake != null) _cameraShake.Changed += i => Edit(d => d.CameraShake = i == 0);
            if (_soundCaptions != null) _soundCaptions.Changed += i => Edit(d => d.SoundCaptions = i == 0);
            _screenMode.Changed += i => Edit(d => d.ScreenMode = (ScreenModeOption)i);
            _resolution.Changed += i => Edit(d => { d.ResolutionWidth = _resolutions[i].x; d.ResolutionHeight = _resolutions[i].y; });
            _language.Changed += i => Edit(d => d.Language = LanguageCodes[i]);
            _defaultsButton.onClick.AddListener(OnDefaults);
            _applyButton.onClick.AddListener(OnApply);
            _closeButton.onClick.AddListener(Close);
            _root.SetActive(false);
            Loc.Changed += OnLanguageChanged;
        }

        private void OnDestroy() => Loc.Changed -= OnLanguageChanged;

        // 적용 직후 선택지 글자도 새 언어로
        private void OnLanguageChanged()
        {
            if (_open) Refresh();
        }

        private static string[] L(string[] ko)
        {
            var r = new string[ko.Length];
            for (int i = 0; i < ko.Length; i++) r[i] = Loc.T(ko[i]);
            return r;
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            _openedAt = Time.unscaledTime;
            _draft = SettingsService.Current.Clone();
            BuildResolutions();
            Refresh();
            SetDirty(false);
            _messageText.text = "";
            _root.SetActive(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _closedFrame = Time.frameCount;
            _root.SetActive(false);
            Closed?.Invoke();
        }

        private void Update()
        {
            if (!_open) return;
            if (UiCommon.ClosePressed(_openedAt, out _)) { Close(); return; }
            if (_messageText.text.Length > 0 && Time.unscaledTime >= _messageUntil) _messageText.text = "";
        }

        private void Edit(Action<SettingsData> change)
        {
            change(_draft);
            SetDirty(true);
            RefreshLanguageNote();
        }

        private void OnDefaults()
        {
            string language = _draft.Language;
            _draft = SettingsService.CreateDefaults();
            _draft.Language = language; // 기본값은 조작·화면·소리만 — 언어까지 시스템 기준으로 바뀌면 읽던 글이 갑자기 바뀐다 (고양이 323)
            Refresh();
            SetDirty(true);
            ShowMessage(Loc.T("기본값으로 바꿨어요. [적용]을 눌러야 저장돼요."), UiColorRole.TextMuted);
        }

        private void OnApply()
        {
            SettingsService.Apply(_draft);
            SetDirty(false);
            ShowMessage(Loc.T("적용했어요."), UiColorRole.PositiveText);
        }

        private void Refresh()
        {
            _sensitivity.SetValue(_draft.MouseSensitivity);
            _masterVolume.SetValue(_draft.MasterVolume);
            _sfxVolume.SetValue(_draft.SfxVolume);
            _musicVolume.SetValue(_draft.MusicVolume);
            _voiceVolume.SetValue(_draft.VoiceVolume);
            _crouch.SetOptions(L(CrouchOptions), _draft.CrouchToggle ? 1 : 0);
            if (_cameraShake != null) _cameraShake.SetOptions(L(ShakeOptions), _draft.CameraShake ? 0 : 1);
            if (_soundCaptions != null) _soundCaptions.SetOptions(L(ShakeOptions), _draft.SoundCaptions ? 0 : 1); // 켬·끔 같은 목록
            _screenMode.SetOptions(L(ScreenModeOptions), (int)_draft.ScreenMode);
            _language.SetOptions(LanguageOptions, Mathf.Max(0, Array.IndexOf(LanguageCodes, _draft.Language)));

            var names = new string[_resolutions.Count];
            for (int i = 0; i < names.Length; i++) names[i] = $"{_resolutions[i].x} × {_resolutions[i].y}";
            _resolution.SetOptions(names, FindResolutionIndex());
            RefreshLanguageNote();
        }

        // 모니터가 지원하는 크기(주사율 중복 제거) + 지금 크기
        private void BuildResolutions()
        {
            _resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!_resolutions.Contains(size)) _resolutions.Add(size);
            }
            var current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            if (!_resolutions.Contains(current)) _resolutions.Add(current);
            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        private int FindResolutionIndex()
        {
            var want = _draft.ResolutionWidth > 0
                ? new Vector2Int(_draft.ResolutionWidth, _draft.ResolutionHeight)
                : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            int index = _resolutions.IndexOf(want);
            return index >= 0 ? index : _resolutions.Count - 1;
        }

        private void RefreshLanguageNote()
        {
            // 고른 언어가 아직 적용 전이면 안내 — 적용하면 바로 바뀐다
            string note = _draft.Language == Loc.Language ? "" : Loc.T("[적용]을 누르면 언어가 바뀌어요.");
            if (_languageNote.text != note) _languageNote.text = note;
        }

        private void SetDirty(bool dirty) => _applyButton.interactable = dirty;

        private void ShowMessage(string message, UiColorRole role)
        {
            _messageText.text = message;
            if (_theme != null) _messageText.color = _theme.GetColor(role);
            _messageUntil = Time.unscaledTime + _messageSeconds;
        }
    }
}
