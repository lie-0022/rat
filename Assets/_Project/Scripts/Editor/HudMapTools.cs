using RatGame.Data;
using RatGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// HUD 벽 속 지도 붙이기 (고양이 102). Hud 프리팹에 MapArea(전체 늘림) → 가운데 창(제목 + 지도 영역) + 방·점 템플릿.
    /// 다시 실행하면 MapArea만 새로 만든다.
    /// </summary>
    public static class HudMapTools
    {
        private const string HudPath = "Assets/_Project/Prefabs/UI/Hud.prefab";

        [MenuItem("Tools/RatGame/UI/Build Walls Map")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                UiThemeSO theme = null;
                foreach (var t in root.GetComponentsInChildren<ThemedGraphic>(true))
                {
                    theme = new SerializedObject(t).FindProperty("_theme").objectReferenceValue as UiThemeSO;
                    if (theme != null) break;
                }
                var old = root.transform.Find("MapArea");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var area = NewUi("MapArea", root.transform);
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = Vector2.zero; area.offsetMax = Vector2.zero;
                // 핑 마커·의심 표시(월드 위 HUD)보다 위, 결과·일시정지·로딩보다 아래 — 지도 위에 마커가 겹쳐 보이지 않게 (고양이 118)
                var above = root.transform.Find("SuspicionArea") ?? root.transform.Find("PingArea") ?? root.transform.Find("HelpArea");
                if (above != null) area.SetSiblingIndex(above.GetSiblingIndex() + 1);

                var panel = NewUi("Panel", area);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(720f, 740f);
                var bg = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
                bg.raycastTarget = false;
                panel.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Panel, UiTextRole.None);
                bg.color = new Color(0.08f, 0.08f, 0.1f, 0.82f);

                var title = NewUi("Title", panel);
                title.anchorMin = new Vector2(0f, 1f); title.anchorMax = new Vector2(1f, 1f); title.pivot = new Vector2(0.5f, 1f);
                title.offsetMin = new Vector2(20f, -60f); title.offsetMax = new Vector2(-20f, -12f);
                var tmp = title.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = "벽 속 지도  <size=70%>(가 본 방만 · 주황 = 목적지 · 금색 = 보물방 · 파란 선 = 배관)</size>";
                tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
                title.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.AccentText, UiTextRole.Heading);

                var content = NewUi("Content", panel);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
                content.anchoredPosition = new Vector2(0f, -24f);
                content.sizeDelta = new Vector2(640f, 640f);

                var room = NewUi("RoomTemplate", content).gameObject.AddComponent<UnityEngine.UI.Image>();
                room.raycastTarget = false;
                var dot = NewUi("DotTemplate", content).gameObject.AddComponent<UnityEngine.UI.Image>();
                dot.raycastTarget = false;
                dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // 둥근 점

                area.gameObject.AddComponent<WallsMapWidget>().EditorSetup(panel.gameObject, content, room, dot);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("[HudMap] 벽 속 지도 — Hud/MapArea");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
