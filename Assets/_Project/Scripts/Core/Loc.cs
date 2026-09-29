using System;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Core
{
    /// <summary>
    /// 문구 번역 (docs/12). 한국어 원문을 넘기면 지금 언어의 문구를 돌려준다 — 번역이 없으면 원문 그대로.
    /// 언어가 바뀌면 <see cref="Changed"/> — 표시 중인 문구는 이걸 듣고 다시 그린다(LocalizedText).
    /// Core에 둔다 — 게임 물체가 내놓는 안내 글자(E 안내 등)도 여기서 번역해야 해서 (UI를 부르면 docs/02 위반, 고양이 192).
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

        // 호스트가 만든 문장 (고양이 202) — 호스트 언어로 완성해 보내면 클라가 번역을 못 한다.
        // 한국어 틀과 값을 구분 문자로 묶어 보내고, 받는 쪽이 자기 언어로 푼다. 여러 문장은 RecordSep로 잇는다(목록).
        public const char ArgSep = '\u001F', RecordSep = '\u001E';

        public static string Pack(string koFormat, params object[] args) =>
            args.Length == 0 ? koFormat : koFormat + ArgSep + string.Join(ArgSep.ToString(), args);

        /// <summary>Pack한 글자(또는 그냥 한국어)를 지금 언어로. 값도 한국어 원문이면 번역(물건 이름 등), 숫자는 그대로.</summary>
        public static string Unpack(string packed)
        {
            if (string.IsNullOrEmpty(packed)) return packed;
            if (packed.IndexOf(RecordSep) >= 0)
            {
                var records = packed.Split(RecordSep);
                for (int i = 0; i < records.Length; i++) records[i] = UnpackOne(records[i]);
                return string.Join(", ", records);
            }
            return UnpackOne(packed);
        }

        private static string UnpackOne(string s)
        {
            var parts = s.Split(ArgSep);
            if (parts.Length == 1) return T(s);
            var args = new object[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++) args[i - 1] = T(parts[i]);
            return F(parts[0], args);
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
