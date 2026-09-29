using System;
using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 문구 번역 (docs/12). 한국어 원문을 넘기면 지금 언어의 문구를 돌려준다 — 번역이 없으면 원문 그대로.
    /// 언어가 바뀌면 <see cref="Changed"/> — 표시 중인 문구는 이걸 듣고 다시 그린다(LocalizedText).
    /// </summary>
    public static class Loc
    {
        private static LocalizationTableSO _table;
        private static bool _loaded;
        private static string _language;

        public static event Action Changed;

        public static bool IsKorean => Language == "ko";

        public static string Language
        {
            get
            {
                if (_language == null) _language = SettingsService.Current.Language ?? "ko";
                return _language;
            }
        }

        public static string T(string ko)
        {
            if (string.IsNullOrEmpty(ko) || IsKorean) return ko;
            var table = Table;
            return table != null && table.TryGetEn(ko, out var en) ? en : ko;
        }

        /// <summary>숫자·이름이 끼는 문장 — 원문 틀("누계 {0}")을 번역한 뒤 채운다. 틀이 곧 키.</summary>
        public static string F(string koFormat, params object[] args)
        {
            try { return string.Format(T(koFormat), args); }
            catch (FormatException) { return string.Format(koFormat, args); } // 번역 틀이 잘못돼도 한국어로는 보이게
        }

        private static LocalizationTableSO Table
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _table = Resources.Load<LocalizationTableSO>("LocalizationTable");
                    if (_table == null) Log.DevWarn("번역 표 없음 (Resources/LocalizationTable) — 한국어로 표시");
                }
                return _table;
            }
        }

        private static void OnSettingsChanged()
        {
            string now = SettingsService.Current.Language ?? "ko";
            if (now == _language) return;
            _language = now;
            Log.Dev($"언어 바뀜: {now}");
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SettingsService.Changed -= OnSettingsChanged;
            SettingsService.Changed += OnSettingsChanged;
            _language = null;
            _table = null;
            _loaded = false;
            Changed = null;
        }
    }
}
