using RatGame.Core;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RatGame.Data;
using RatGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 메뉴 화면 번역 준비 (docs/12 언어). 번역 표를 만들고, 메뉴 프리팹의 고정 한국어 글자에 LocalizedText를 붙이고,
    /// 표에 원문을 모은다. 영어는 임시(더미) 번역 — 문구 정리 작업 때 표에서 고친다. 다시 돌려도 이미 있는 건 건드리지 않는다.
    /// </summary>
    public static partial class LocalizationTools
    {
        private const string TablePath = "Assets/_Project/Resources/LocalizationTable.asset";

        // UI 프리팹 전부 (메뉴 3종으로 시작 — 고양이 188, HUD·패널까지 — 고양이 189)
        private const string UiPrefabFolder = "Assets/_Project/Prefabs/UI";

        // 스크립트가 들고 있지만 글자는 안 쓰는 것 (색·위치만 읽음) — 번역해도 된다
        private static readonly HashSet<string> StaticRefs = new() { "ControlsHelpWidget._title", "LoadingOverlayWidget._waiting" };

        // 코드가 복제해 글자를 새로 쓰는 틀(핑 이름·알림·결과 칩) 또는 Find로 찾아 덮어쓰는 글자 — 붙이면 켜질 때 원문으로 되돌린다
        private static readonly HashSet<string> CodeTemplates = new() { "회색 쥐", "도감 등록!  계란", "계란", "쥐구멍 적립" };

        private static readonly Regex Hangul = new("[가-힣]");

        [MenuItem("Tools/RatGame/UI/Localize Menus")]
        public static void LocalizeMenus()
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTableSO>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LocalizationTableSO>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            int added = 0, attached = 0;
            foreach (var kv in DummyEn)
                if (table.EditorAdd(kv.Key, kv.Value)) added++;

            var missingEn = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var codeDriven = CollectCodeDriven(root);
                bool changed = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!Hangul.IsMatch(text.text) || codeDriven.Contains(text)) continue;
                    if (CodeTemplates.Contains(text.text))
                    {
                        var wrong = text.GetComponent<LocalizedText>();
                        if (wrong != null) { Object.DestroyImmediate(wrong, true); changed = true; }
                        // 표에서는 안 지운다 — 같은 글자(쥐 색·물건 이름)를 코드가 Loc.T로 쓴다 (고양이 192)
                        continue;
                    }
                    // 중첩 프리팹(설정 패널 안의 행 등)은 그 프리팹 쪽에서 붙인다
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;
                    if (table.EditorAdd(text.text, DummyEn.TryGetValue(text.text, out var en) ? en : "")) added++;
                    if (string.IsNullOrEmpty(en)) missingEn.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}/{text.name}: {text.text.Replace("\n", "\\n")}");
                    var loc = text.GetComponent<LocalizedText>();
                    if (loc != null && loc.Ko == text.text) continue;
                    if (loc == null) { loc = text.gameObject.AddComponent<LocalizedText>(); attached++; }
                    loc.Ko = text.text;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            int missing = 0, badSlots = 0;
            foreach (var e in table.Entries)
            {
                if (string.IsNullOrEmpty(e.En)) { missing++; continue; }
                // Loc.F 틀 — 영어에 {n} 자리가 하나라도 다르면 string.Format이 게임 중에 터진다
                if (Slots(e.Ko) != Slots(e.En)) { badSlots++; Debug.LogError($"[Rat] 번역 자리 불일치: \"{e.Ko}\" → \"{e.En}\""); }
            }
            foreach (var m in missingEn) Debug.Log($"[Rat] 번역 없음 — {m}");
            Debug.Log($"[Rat] 메뉴 번역 준비: 표 {table.Entries.Count}줄(새 {added}, 영어 빈 칸 {missing}, 자리 불일치 {badSlots}) · LocalizedText 새로 {attached}개");
        }

        private static readonly Regex Slot = new(@"\{\d+[^}]*\}");

        private static string Slots(string s)
        {
            var list = new List<string>();
            foreach (Match m in Slot.Matches(s)) list.Add(m.Value);
            list.Sort(System.StringComparer.Ordinal);
            return string.Join(",", list);
        }

        // 스크립트가 [SerializeField]로 들고 있는 글자 = 코드가 쓰는 글자 (LocalizedText를 붙이면 원문이 엉뚱하게 잡힌다)
        private static HashSet<TMP_Text> CollectCodeDriven(GameObject root)
        {
            var set = new HashSet<TMP_Text>();
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is TMP_Text) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (StaticRefs.Contains($"{mb.GetType().Name}.{it.name}")) continue;
                    if (it.objectReferenceValue is TMP_Text t && set.Add(t) && Hangul.IsMatch(t.text))
                        Debug.Log($"[Rat] 번역 도구: 코드가 쓰는 글자로 보고 뺌 — {mb.GetType().Name}.{it.name} \"{t.text}\"");
                }
            }
            return set;
        }
    }
}
