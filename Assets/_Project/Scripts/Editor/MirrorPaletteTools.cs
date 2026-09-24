using RatGame.Data;
using RatGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 거울 패널에 털 색 팔레트 붙이기 (2026-09-24). 닫기 버튼 위에 "Palette" 묶음을 넣는다 — 견본 16개(누르면 바로 입음),
    /// 색상·채도·밝기 슬라이더, 미리보기 + 색 코드 입력칸 + "이 색 입기" + 미리보기 안내. 다시 실행하면 Palette 묶음만 새로 만든다.
    /// 창은 화면 오른쪽에 붙이고 배경 어둡게 하기를 약하게 — 가운데 거울에 비친 내 쥐가 보이게.
    /// </summary>
    public static class MirrorPaletteTools
    {
        private const string PanelPath = "Assets/_Project/Prefabs/UI/MirrorPanel.prefab";

        // 견본: 팀 기본 4색 + 쥐다운 색 + 튀는 색. 겹쳐도 된다(사용자 결정)
        private static readonly Color[] Swatches =
        {
            new(0.80f, 0.80f, 0.84f), new(0.55f, 0.70f, 0.95f), new(0.60f, 0.88f, 0.55f), new(0.95f, 0.85f, 0.45f),
            new(0.97f, 0.96f, 0.94f), new(0.45f, 0.45f, 0.48f), new(0.18f, 0.17f, 0.20f), new(0.55f, 0.36f, 0.22f),
            new(0.86f, 0.74f, 0.58f), new(0.93f, 0.33f, 0.30f), new(0.96f, 0.58f, 0.25f), new(0.98f, 0.62f, 0.78f),
            new(0.62f, 0.45f, 0.85f), new(0.35f, 0.80f, 0.85f), new(0.25f, 0.55f, 0.35f), new(0.20f, 0.30f, 0.55f),
        };

        [MenuItem("Tools/RatGame/UI/Build Mirror Palette")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var window = root.transform.Find("Dim/Window");
                var close = window != null ? window.Find("CloseButton") : null;
                if (close == null) { Debug.LogError("[MirrorPalette] Dim/Window/CloseButton 없음"); return; }
                var theme = FindTheme(root);

                var old = window.Find("Palette");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var palette = NewUi("Palette", window);
                palette.SetSiblingIndex(close.GetSiblingIndex());
                var vlg = palette.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                vlg.spacing = 10; vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

                var title = Text("PaletteTitle", palette, "털 색 팔레트", theme, UiColorRole.AccentText, UiTextRole.Heading, 34f);
                title.alignment = TextAlignmentOptions.Center;

                // 견본 격자 8×2
                var grid = NewUi("Swatches", palette);
                var gl = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                gl.cellSize = new Vector2(44, 44); gl.spacing = new Vector2(10, 10);
                gl.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 8;
                gl.childAlignment = TextAnchor.UpperCenter;
                Height(grid, 98f);
                for (int i = 0; i < Swatches.Length; i++)
                {
                    var sw = NewUi($"Swatch_{i:00}", grid);
                    var img = sw.gameObject.AddComponent<UnityEngine.UI.Image>();
                    img.color = Swatches[i];
                    sw.gameObject.AddComponent<UnityEngine.UI.Button>().targetGraphic = img;
                }

                var hue = SliderRow("HueRow", "색상", palette, theme);
                var sat = SliderRow("SaturationRow", "채도", palette, theme);
                var val = SliderRow("ValueRow", "밝기", palette, theme);

                // 미리보기 · #RRGGBB · 입기
                var applyRow = NewUi("ApplyRow", palette);
                var hlg = applyRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
                Height(applyRow, 46f);
                var preview = NewUi("Preview", applyRow);
                var previewImg = preview.gameObject.AddComponent<UnityEngine.UI.Image>();
                previewImg.raycastTarget = false;
                Size(preview, 120f, 44f);
                var hex = HexInput(applyRow, theme);
                var apply = NewUi("ApplyButton", applyRow);
                var applyImg = apply.gameObject.AddComponent<UnityEngine.UI.Image>();
                apply.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Accent, UiTextRole.None);
                var applyButton = apply.gameObject.AddComponent<UnityEngine.UI.Button>();
                applyButton.targetGraphic = applyImg;
                Size(apply, 170f, 46f);
                var applyText = Text("Text", apply, "이 색 입기", theme, UiColorRole.OnAccent, UiTextRole.Body, -1f);
                applyText.alignment = TextAlignmentOptions.Center;
                Stretch(applyText.rectTransform);

                var hint = Text("PreviewHint", palette, "", theme, UiColorRole.TextMuted, UiTextRole.Small, 24f);
                hint.alignment = TextAlignmentOptions.Center;

                // 전체 높이 = 제목 34 + 격자 98 + 슬라이더 3×32 + 입기 46 + 안내 24 + 간격 6×10
                Height(palette, 34f + 98f + 3 * 32f + 46f + 24f + 6 * 10f);
                var winRect = (RectTransform)window;
                winRect.anchorMin = winRect.anchorMax = new Vector2(1f, 0.5f);
                winRect.pivot = new Vector2(1f, 0.5f);
                winRect.anchoredPosition = new Vector2(-60f, 0f);
                winRect.sizeDelta = new Vector2(600f, 880f);
                // 거울이 보이게 배경을 살짝만 어둡게 (클릭 막이는 그대로)
                var dim = window.parent;
                var dimTheme = dim.GetComponent<ThemedGraphic>();
                if (dimTheme != null) dimTheme.Setup(theme, UiColorRole.None, UiTextRole.None);
                dim.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.15f);
                var titleTf = window.Find("TitleText");
                var titleText = titleTf != null ? titleTf.GetComponent<TMP_Text>() : null;
                if (titleText != null) titleText.text = "거울 — 스킨·털 색";

                root.GetComponent<MirrorPanel>().EditorSetupPalette(grid, hue, sat, val, previewImg, hex, applyButton, hint);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"[MirrorPalette] 팔레트 — 견본 {Swatches.Length}, 슬라이더 3, 창 오른쪽 600×880");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

        private static void Height(RectTransform rt, float h)
        {
            var le = rt.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = h; le.minHeight = h;
        }

        private static void Size(RectTransform rt, float w, float h)
        {
            Height(rt, h);
            var le = rt.GetComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = w; le.minWidth = w;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, UiThemeSO theme, UiColorRole color, UiTextRole role, float height)
        {
            var rt = NewUi(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.raycastTarget = false;
            rt.gameObject.AddComponent<ThemedGraphic>().Setup(theme, color, role);
            if (height > 0f) Height(rt, height);
            return tmp;
        }

        private static UnityEngine.UI.Slider SliderRow(string name, string label, Transform parent, UiThemeSO theme)
        {
            var row = NewUi(name, parent);
            var hlg = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            Height(row, 32f);
            var text = Text("Label", row, label, theme, UiColorRole.Text, UiTextRole.Body, 32f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var le = text.GetComponent<UnityEngine.UI.LayoutElement>(); le.preferredWidth = 70f; le.minWidth = 70f;

            var sliderGo = UnityEngine.UI.DefaultControls.CreateSlider(new UnityEngine.UI.DefaultControls.Resources());
            sliderGo.name = "Slider";
            sliderGo.layer = row.gameObject.layer;
            foreach (Transform t in sliderGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = row.gameObject.layer;
            var srt = (RectTransform)sliderGo.transform;
            srt.SetParent(row, false);
            var sle = sliderGo.AddComponent<UnityEngine.UI.LayoutElement>();
            sle.flexibleWidth = 1f; sle.preferredHeight = 22f; sle.minHeight = 22f;
            var slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.5f;
            // 색은 테마 역할로 (배경 = 줄, 채움 = 강조, 손잡이 = 글자색)
            Theme(sliderGo.transform.Find("Background"), theme, UiColorRole.Row);
            Theme(sliderGo.transform.Find("Fill Area/Fill"), theme, UiColorRole.Accent);
            Theme(sliderGo.transform.Find("Handle Slide Area/Handle"), theme, UiColorRole.Text);
            return slider;
        }

        // "#RRGGBB" 입력칸 — TMP 기본 입력칸에 테마 색·글꼴
        private static TMP_InputField HexInput(Transform parent, UiThemeSO theme)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "HexInput";
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.flexibleWidth = 1f; le.preferredHeight = 44f; le.minHeight = 44f;
            var input = go.GetComponent<TMP_InputField>();
            input.characterLimit = 7;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onFocusSelectAll = true;
            Theme(go.transform, theme, UiColorRole.Row);
            var text = input.textComponent;
            text.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Text, UiTextRole.Body);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholder = input.placeholder as TMP_Text;
            if (placeholder != null)
            {
                placeholder.text = "#RRGGBB";
                placeholder.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.TextMuted, UiTextRole.Body);
                placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            }
            input.text = "#FFFFFF";
            return input;
        }

        private static void Theme(Transform t, UiThemeSO theme, UiColorRole role)
        {
            if (t == null) return;
            var tg = t.GetComponent<ThemedGraphic>();
            if (tg == null) tg = t.gameObject.AddComponent<ThemedGraphic>();
            tg.Setup(theme, role, UiTextRole.None);
        }
    }
}
