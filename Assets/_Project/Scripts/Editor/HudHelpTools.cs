using RatGame.Data;
using RatGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Editor
{
    /// <summary>
    /// HUD 조작 안내 붙이기 (고양이 77). Hud 프리팹에 HelpArea(전체 늘림) → 왼쪽 위 팀 상자 아래 "F1 조작" 꼬리표 + 펼치는 목록 창.
    /// 다시 실행하면 HelpArea만 새로 만든다.
    /// </summary>
    public static class HudHelpTools
    {
        private const string HudPath = "Assets/_Project/Prefabs/UI/Hud.prefab";
        private const string InputPath = "Assets/_Project/Settings/RatInput.inputactions";

        [MenuItem("Tools/RatGame/UI/Build Controls Help")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var theme = FindTheme(root);
                var old = root.transform.Find("HelpArea");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var area = NewUi("HelpArea", root.transform);
                Stretch(area);
                var team = root.transform.Find("TeamStatusArea");
                if (team != null) area.SetSiblingIndex(team.GetSiblingIndex() + 1);

                // 꼬리표: 팀 상자(위 24~118) 아래
                var tag = Box("Tag", area, theme, new Vector2(24f, -128f), new Vector2(150f, 40f));
                var tagText = Text("Text", tag, "<b>F1</b>  조작", theme, UiColorRole.TextMuted, UiTextRole.Small);
                Stretch(tagText.rectTransform, 12f);

                var panel = Box("Panel", area, theme, new Vector2(24f, -128f), new Vector2(430f, 520f));
                var title = Text("Title", panel, "조작  <size=70%>(F1 닫기)</size>", theme, UiColorRole.AccentText, UiTextRole.Heading);
                var trt = title.rectTransform;
                trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
                trt.offsetMin = new Vector2(18f, -60f); trt.offsetMax = new Vector2(-18f, -12f);
                var list = Text("List", panel, "", theme, UiColorRole.Text, UiTextRole.Body);
                var lrt = list.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(18f, 14f); lrt.offsetMax = new Vector2(-18f, -64f);
                list.alignment = TextAlignmentOptions.TopLeft;
                list.lineSpacing = 12f;
                list.enableAutoSizing = true; list.fontSizeMin = 16f; list.fontSizeMax = 26f; // 키가 늘어도 창 안에

                var widget = area.gameObject.AddComponent<ControlsHelpWidget>();
                widget.EditorSetup(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath), tag.gameObject, panel.gameObject, list, title, team != null ? team.Find("Box") as RectTransform : null);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("[HudHelp] 조작 안내 — 꼬리표 + 목록 창 (Hud/HelpArea)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform Box(string name, Transform parent, UiThemeSO theme, Vector2 topLeft, Vector2 size)
        {
            var rt = NewUi(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            rt.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Panel, UiTextRole.None);
            return rt;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, UiThemeSO theme, UiColorRole color, UiTextRole role)
        {
            var rt = NewUi(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            rt.gameObject.AddComponent<ThemedGraphic>().Setup(theme, color, role);
            return tmp;
        }

        private static UiThemeSO FindTheme(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<ThemedGraphic>(true))
            {
                var theme = new SerializedObject(t).FindProperty("_theme").objectReferenceValue as UiThemeSO;
                if (theme != null) return theme;
            }
            return null;
        }

        private static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, 0f); rt.offsetMax = new Vector2(-inset, 0f);
        }
    }
}
