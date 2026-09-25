using System;
using System.IO;
using UnityEngine;

namespace RatGame.Core
{
    /// <summary>화면 모드 선택지 (설정 패널 순서 = 값).</summary>
    public enum ScreenModeOption { FullScreen, Borderless, Windowed }

    /// <summary>기기별 설정 (docs/12 설정). 세이브(진행)와 따로 둔다 — 설정을 초기화해도 진행이 안 날아가게.</summary>
    [Serializable]
    public class SettingsData
    {
        public int Version = 1;
        /// <summary>시선 감도 배율 (기본 1.0 = PlayerCameraRig 기준 감도).</summary>
        public float MouseSensitivity = 1f;
        /// <summary>웅크리기: false = 누르고 있기(docs/04 기본), true = 눌러서 전환.</summary>
        public bool CrouchToggle;
        /// <summary>화면 흔들림(큰 고양이 발걸음 등) — 멀미 배려로 끌 수 있게 (고양이 145).</summary>
        public bool CameraShake = true;
        public ScreenModeOption ScreenMode = ScreenModeOption.Borderless;
        /// <summary>0이면 모니터 해상도 그대로.</summary>
        public int ResolutionWidth;
        public int ResolutionHeight;
        public float MasterVolume = 1f;
        public float SfxVolume = 1f;
        public float MusicVolume = 1f;
        public float VoiceVolume = 1f;
        /// <summary>"ko" / "en" — 번역 테이블(LocalizationTable SO)은 문구 정리 작업 때.</summary>
        public string Language = "ko";

        public SettingsData Clone() => (SettingsData)MemberwiseClone();
    }

    /// <summary>
    /// 설정 읽기·적용·저장 (docs/02 허용 싱글톤 — SaveService와 같은 정적 서비스). persistentDataPath/settings.json.
    /// 전체 볼륨은 AudioListener에 바로 적용, 효과음·음악·보이스는 오디오 소스가 생기면 이 값을 곱해 쓴다.
    /// </summary>
    public static class SettingsService
    {
        private const string FileName = "settings.json";

        private static SettingsData _current;

        public static event Action Changed;

        public static SettingsData Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SettingsData CreateDefaults() => new SettingsData();

        /// <summary>패널에서 고친 사본을 확정 — 저장하고 바로 적용. 화면 설정은 바뀐 경우에만 건드린다 (깜빡임 방지).</summary>
        public static void Apply(SettingsData data)
        {
            var prev = Current;
            _current = data.Clone();
            bool displayChanged = prev.ScreenMode != _current.ScreenMode
                                  || prev.ResolutionWidth != _current.ResolutionWidth
                                  || prev.ResolutionHeight != _current.ResolutionHeight;
            ApplyAudio();
            if (displayChanged) ApplyDisplay();
            Save();
            Changed?.Invoke();
        }

        private static void Load()
        {
            _current = null;
            try
            {
                if (File.Exists(FilePath)) _current = JsonUtility.FromJson<SettingsData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Log.Error($"설정 파일 읽기 실패: {e.Message}");
            }
            _current ??= CreateDefaults();
            ApplyAudio();
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_current, true));
                Log.Dev($"설정 저장 ({FilePath})");
            }
            catch (Exception e)
            {
                Log.Error($"설정 저장 실패: {e.Message}");
            }
        }

        private static void ApplyAudio() => AudioListener.volume = Mathf.Clamp01(_current.MasterVolume);

        // 해상도·전체 화면은 빌드에서 Unity가 스스로 기억하므로 시작할 때 다시 적용하지 않는다 — 바꿀 때만
        private static void ApplyDisplay()
        {
            var mode = _current.ScreenMode switch
            {
                ScreenModeOption.FullScreen => FullScreenMode.ExclusiveFullScreen,
                ScreenModeOption.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
            int w = _current.ResolutionWidth > 0 ? _current.ResolutionWidth : Screen.currentResolution.width;
            int h = _current.ResolutionHeight > 0 ? _current.ResolutionHeight : Screen.currentResolution.height;
            Screen.SetResolution(w, h, mode);
            Log.Dev($"화면 설정: {w}×{h} {mode}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _current = null;

        // 시작 시 전체 볼륨 반영 (설정 파일 읽기)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadOnStart() => Load();
    }
}
