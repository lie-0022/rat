using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 문구 번역 표 (docs/12 설정 — 언어). 키는 한국어 원문 그대로 — 프리팹·코드의 한국어가 곧 키라서
    /// 번역이 빠진 문구는 한국어로 남을 뿐 깨지지 않는다. Resources/LocalizationTable 하나만 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "RatGame/Localization Table", fileName = "LocalizationTable")]
    public class LocalizationTableSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [TextArea] public string Ko;
            [TextArea] public string En;
        }

        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<string, string> _en;

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>영어가 있으면 true.</summary>
        public bool TryGetEn(string ko, out string en)
        {
            if (_en == null)
            {
                _en = new Dictionary<string, string>(_entries.Count);
                foreach (var e in _entries)
                    if (!string.IsNullOrEmpty(e.Ko) && !string.IsNullOrEmpty(e.En)) _en[e.Ko] = e.En;
            }
            return _en.TryGetValue(ko, out en);
        }

#if UNITY_EDITOR
        /// <summary>에디터 도구용 — 원문을 더하거나 영어를 바꾼다. 빈 영어로는 덮지 않는다. 번역 원본은 도구의 목록 파일(고양이 195).</summary>
        public bool EditorAdd(string ko, string en)
        {
            var found = _entries.Find(e => e.Ko == ko);
            if (found != null)
            {
                if (string.IsNullOrEmpty(en) || found.En == en) return false;
                found.En = en;
            }
            else _entries.Add(new Entry { Ko = ko, En = en });
            _en = null;
            return true;
        }

        public bool EditorRemove(string ko)
        {
            _en = null;
            return _entries.RemoveAll(e => e.Ko == ko) > 0;
        }

        private void OnValidate() => _en = null;
#endif
    }
}
