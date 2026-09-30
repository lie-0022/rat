using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RatGame.Data;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 데이터 에셋(SO)의 한국어 글자도 번역 표에 (고양이 317) — 아이템·업그레이드·스킨·판매대·도전과제 이름과 설명은
    /// 코드가 Loc.T(so.DisplayName)로 번역한다. 표에 없으면 영어 모드에서 그 줄만 한국어로 남는데, 프리팹·코드 검사로는 안 잡힌다.
    /// </summary>
    public class DataTextTests
    {
        private static readonly Regex Hangul = new(@"[가-힣]");

        // 화면에 안 나오는 필드 — 개발 메모·에디터 표시용
        private static readonly string[] SkipFields = { "Note", "Memo", "Comment", "_note", "_memo", "m_Name" };

        private static bool Skip(string name)
        {
            foreach (var s in SkipFields) if (name.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        [Test]
        public void 데이터_에셋의_한국어_글자는_모두_표에()
        {
            var table = Resources.Load<LocalizationTableSO>("LocalizationTable");
            Assert.NotNull(table);
            var missing = new List<string>();
            int checkedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data", "Assets/_Project/Resources" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null || so is LocalizationTableSO) continue;
                var it = new SerializedObject(so).GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.String || Skip(it.name)) continue;
                    if (so is CatPersonalitySO && it.name == "_displayName") continue; // 성격 이름은 개발 로그에만 — 화면엔 문지기·순찰꾼 같은 역할 이름(Loc.T)이 나온다
                    string value = it.stringValue;
                    if (string.IsNullOrEmpty(value) || !Hangul.IsMatch(value)) continue;
                    checkedCount++;
                    if (!table.TryGetEn(value, out _)) missing.Add($"{path} / {it.propertyPath} \"{value}\"");
                }
            }
            Assert.IsEmpty(missing, "번역 표에 없음:\n" + string.Join("\n", missing));
            Assert.Greater(checkedCount, 50);
        }
    }
}
