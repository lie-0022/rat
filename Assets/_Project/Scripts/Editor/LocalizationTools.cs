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
    public static class LocalizationTools
    {
        private const string TablePath = "Assets/_Project/Resources/LocalizationTable.asset";

        private static readonly string[] MenuPrefabs =
        {
            "Assets/_Project/Prefabs/UI/MainMenu.prefab",
            "Assets/_Project/Prefabs/UI/PauseMenu.prefab",
            "Assets/_Project/Prefabs/UI/SettingsPanel.prefab",
        };

        private static readonly Dictionary<string, string> DummyEn = new()
        {
            // 메인 메뉴
            ["쥐도 새도 모르게"] = "Not Even a Mouse",
            ["1~4인 협동 은신 약탈"] = "1-4 player co-op stealth heist",
            ["호스트 시작"] = "Host Game",
            ["친구 방 참가"] = "Join Friend",
            ["설정"] = "Settings",
            ["종료"] = "Quit",
            ["로컬 모드"] = "Local mode",
            ["로컬 모드 (같은 PC에서만 참가)"] = "Local mode (same PC only)",
            [" · 개발 빌드"] = " · dev build",
            ["기지를 여는 중…"] = "Opening the base…",
            ["호스트를 시작하지 못했어요. 이미 켜진 게임이 있는지 확인하세요."] = "Couldn't start hosting. Is another game already running?",
            ["친구가 보낸 초대를 수락하면 바로 들어가요.\n(Steam 친구 목록에서 \"게임 참가\"도 돼요)"] = "Accept a friend's invite to join.\n(\"Join Game\" in the Steam friends list works too)",
            ["기지에 접속하는 중…"] = "Connecting to the base…",
            ["접속을 시작하지 못했어요."] = "Couldn't start connecting.",
            ["호스트를 찾지 못했어요. 친구가 방을 열었는지 확인하세요."] = "Host not found. Did your friend open a room?",
            // 일시정지
            ["일시정지"] = "Paused",
            ["계속하기"] = "Resume",
            ["세션 나가기"] = "Leave Session",
            ["나가기"] = "Leave",
            ["취소"] = "Cancel",
            ["호스트가 나가면 친구들의 세션도 끝나요.\n메인 메뉴로 나갈까요?"] = "If the host leaves, everyone's session ends.\nReturn to main menu?",
            ["메인 메뉴로 나갈까요?\n친구들은 계속 플레이해요."] = "Return to main menu?\nYour friends keep playing.",
            ["내가 연 방"] = "My room",
            ["친구 방"] = "Friend's room",
            ["명"] = " players",
            // 설정
            ["조작"] = "Controls",
            ["마우스 감도"] = "Mouse sensitivity",
            ["웅크리기"] = "Crouch",
            ["화면 흔들림"] = "Camera shake",
            ["화면"] = "Display",
            ["화면 모드"] = "Screen mode",
            ["해상도"] = "Resolution",
            ["언어"] = "Language",
            ["소리"] = "Audio",
            ["전체"] = "Master",
            ["효과음"] = "Effects",
            ["음악"] = "Music",
            ["보이스"] = "Voice",
            ["기본값"] = "Defaults",
            ["닫기"] = "Close",
            ["적용"] = "Apply",
            ["누르고 있기"] = "Hold",
            ["눌러서 전환"] = "Toggle",
            ["켬"] = "On",
            ["끔"] = "Off",
            ["전체 화면"] = "Fullscreen",
            ["테두리 없는 창"] = "Borderless",
            ["창 모드"] = "Windowed",
            ["기본값으로 바꿨어요. [적용]을 눌러야 저장돼요."] = "Reset to defaults. Press [Apply] to save.",
            ["적용했어요."] = "Applied.",
            ["[적용]을 누르면 언어가 바뀌어요."] = "Press [Apply] to change the language.",
        };

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

            foreach (var path in MenuPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var codeDriven = CollectCodeDriven(root);
                bool changed = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!Hangul.IsMatch(text.text) || codeDriven.Contains(text)) continue;
                    // 중첩 프리팹(설정 패널 안의 행 등)은 그 프리팹 쪽에서 붙인다
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;
                    if (table.EditorAdd(text.text, DummyEn.TryGetValue(text.text, out var en) ? en : "")) added++;
                    if (text.GetComponent<LocalizedText>() != null) continue;
                    text.gameObject.AddComponent<LocalizedText>();
                    attached++;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            int missing = 0;
            foreach (var e in table.Entries) if (string.IsNullOrEmpty(e.En)) missing++;
            Debug.Log($"[Rat] 메뉴 번역 준비: 표 {table.Entries.Count}줄(새 {added}, 영어 빈 칸 {missing}) · LocalizedText 새로 {attached}개");
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
                    if (it.objectReferenceValue is TMP_Text t && set.Add(t) && Hangul.IsMatch(t.text))
                        Debug.Log($"[Rat] 번역 도구: 코드가 쓰는 글자로 보고 뺌 — {mb.GetType().Name}.{it.name} \"{t.text}\"");
                }
            }
            return set;
        }
    }
}
