using RatGame.Data;
using RatGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// HUD 내 발소리 크기 표시 붙이기 (고양이 170). Hud 프리팹에 NoiseArea(전체 늘림) → 오른쪽 아래 작은 상자 + 글자.
    /// 다시 실행하면 NoiseArea만 새로 만든다.
    /// </summary>
    public static class HudNoiseTools
    {
        private const string HudPath = "Assets/_Project/Prefabs/UI/Hud.prefab";
        private const string BalancePath = "Assets/_Project/Data/Balance/BalanceConfig.asset";

        [MenuItem("Tools/RatGame/UI/Build Noise Meter")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                UiThemeSO theme = null;
                foreach (var tg in root.GetComponentsInChildren<ThemedGraphic>(true))
                {
                    theme = new SerializedObject(tg).FindProperty("_theme").objectReferenceValue as UiThemeSO;
                    if (theme != null) break;
                }
                var old = root.transform.Find("NoiseArea");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var area = NewUi("NoiseArea", root.transform);
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
                var slots = root.transform.Find("SlotsArea");
                if (slots != null) area.SetSiblingIndex(slots.GetSiblingIndex() + 1);

                // 오른쪽 아래 — 토스트(오른쪽 위)·팀 상자(왼쪽 위)·슬롯(아래 가운데)과 안 겹치게
                var box = NewUi("Box", area);
                box.anchorMin = box.anchorMax = new Vector2(1f, 0f);
                box.pivot = new Vector2(1f, 0f);
                box.anchoredPosition = new Vector2(-24f, 24f);
                box.sizeDelta = new Vector2(220f, 44f);
                var img = box.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
                box.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Panel, UiTextRole.None);

                var textRt = NewUi("Text", box);
                textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(14f, 0f); textRt.offsetMax = new Vector2(-14f, 0f);
                var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = "소리 · 조용";
                tmp.raycastTarget = false;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                textRt.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.TextMuted, UiTextRole.Small);

                var widget = area.gameObject.AddComponent<NoiseMeterWidget>();
                widget.EditorSetup(AssetDatabase.LoadAssetAtPath<BalanceConfigSO>(BalancePath), theme, tmp, box.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("[HudNoise] 내 발소리 크기 표시 — 오른쪽 아래 (Hud/NoiseArea)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
