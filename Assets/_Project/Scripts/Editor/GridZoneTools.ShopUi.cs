using RatGame.Data;
using RatGame.UI;
using RatGame.World;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 목적지 상점 판매대·패널 (plan cat-65): 판매대 프리팹(네트워크), StageShopPanel·StageShopRow UI 프리팹(거울 패널과 같은 캔버스·테마),
    /// Hud의 WorldPanelHost와 벽 속 테마에 연결. 다시 실행하면 덮어쓴다.
    /// </summary>
    public static partial class GridZoneTools
    {
        private const string CounterPath = "Assets/_Project/Prefabs/Zones/StageShopCounter.prefab";
        private const string PanelPath = "Assets/_Project/Prefabs/UI/StageShopPanel.prefab";
        private const string RowPath = "Assets/_Project/Prefabs/UI/StageShopRow.prefab";

        [MenuItem("Tools/RatGame/Zone/Create Stage Shop Counter & Panel")]
        public static void CreateStageShopUi()
        {
            var shop = AssetDatabase.LoadAssetAtPath<StageShopSO>(ShopPath);
            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            var theme = FindTheme();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Jua/Jua SDF.asset");

            // ① 판매대 (월드)
            var counter = new GameObject("StageShopCounter");
            var box = counter.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.5f, 0f); box.size = new Vector3(1.2f, 1f, 0.6f);
            counter.AddComponent<Unity.Netcode.NetworkObject>();
            counter.AddComponent<StageShopCounter>().EditorSetup(shop);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body"; Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(counter.transform, false); body.transform.localPosition = new Vector3(0f, 0.5f, 0f); body.transform.localScale = new Vector3(1.2f, 1f, 0.6f);
            ZoneTools.Tint(body, new Color(0.62f, 0.42f, 0.25f));
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(counter.transform, false); labelGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            var label = labelGo.AddComponent<TextMeshPro>(); label.font = font; label.fontSize = 0.8f; label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.6f); label.text = "[E] 상점";
            zone.ShopCounterPrefab = PrefabUtility.SaveAsPrefabAsset(counter, CounterPath);
            Object.DestroyImmediate(counter);

            // ② 한 줄 프리팹
            var row = NewRect("StageShopRow", null);
            var rowImg = row.gameObject.AddComponent<UnityEngine.UI.Image>(); rowImg.raycastTarget = false;
            row.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Row, UiTextRole.None);
            var rowHlg = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            rowHlg.padding = new RectOffset(16, 16, 8, 8); rowHlg.spacing = 12; rowHlg.childAlignment = TextAnchor.MiddleLeft;
            rowHlg.childControlWidth = true; rowHlg.childControlHeight = true; rowHlg.childForceExpandWidth = false; rowHlg.childForceExpandHeight = false;
            Le(row, -1, 72);
            var textCol = NewRect("TextColumn", row);
            var col = textCol.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandHeight = false; col.spacing = 2;
            textCol.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            var nameText = Tmp("Name", textCol, "이름", theme, UiColorRole.Text, UiTextRole.Heading, 30);
            var descText = Tmp("Desc", textCol, "설명", theme, UiColorRole.TextMuted, UiTextRole.Small, 22);
            var priceText = Tmp("Price", row, "0 식량", theme, UiColorRole.AccentText, UiTextRole.Body, 40); Le(priceText.rectTransform, 110, 40);
            var buy = NewRect("BuyButton", row);
            var buyImg = buy.gameObject.AddComponent<UnityEngine.UI.Image>();
            buy.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Accent, UiTextRole.None);
            var buyBtn = buy.gameObject.AddComponent<UnityEngine.UI.Button>(); buyBtn.targetGraphic = buyImg;
            Le(buy, 120, 46);
            var buyText = Tmp("Text", buy, "사기", theme, UiColorRole.OnAccent, UiTextRole.Body, -1); Fill(buyText.rectTransform); buyText.alignment = TextAlignmentOptions.Center;
            row.gameObject.AddComponent<StageShopRow>().EditorSetup(nameText, descText, priceText, buyBtn);
            var rowPrefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPath).GetComponent<StageShopRow>();
            Object.DestroyImmediate(row.gameObject);

            // ③ 패널 (거울 패널과 같은 캔버스: 오버레이 정렬 20, 1920×1080)
            var root = new GameObject("StageShopPanel", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var dim = NewRect("Dim", (RectTransform)root.transform); Fill(dim);
            dim.gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Dim, UiTextRole.None);
            var window = NewRect("Window", dim); window.sizeDelta = new Vector2(680, 540);
            window.gameObject.AddComponent<UnityEngine.UI.Image>();
            window.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Panel, UiTextRole.None);
            var vlg = window.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 24, 24); vlg.spacing = 12; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            Tmp("TitleText", window, "상점 — 다음 맵 준비", theme, UiColorRole.AccentText, UiTextRole.Title, 48);
            var wallet = Tmp("WalletText", window, "쓸 수 있는 식량 0", theme, UiColorRole.Text, UiTextRole.Body, 30);
            var rows = NewRect("Rows", window);
            var rv = rows.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); rv.spacing = 8; rv.childControlWidth = true; rv.childControlHeight = true; rv.childForceExpandHeight = false;
            Le(rows, -1, 3 * 72 + 2 * 8);
            var pending = Tmp("PendingText", window, "다음 맵에서 받을 것: 없음", theme, UiColorRole.TextMuted, UiTextRole.Small, 26);
            var message = Tmp("MessageText", window, "", theme, UiColorRole.None, UiTextRole.Body, 28);
            var close = NewRect("CloseButton", window);
            var closeImg = close.gameObject.AddComponent<UnityEngine.UI.Image>();
            close.gameObject.AddComponent<ThemedGraphic>().Setup(theme, UiColorRole.Secondary, UiTextRole.None);
            var closeBtn = close.gameObject.AddComponent<UnityEngine.UI.Button>(); closeBtn.targetGraphic = closeImg;
            Le(close, -1, 52);
            var closeText = Tmp("Text", close, "닫기 (E / Esc)", theme, UiColorRole.Text, UiTextRole.Body, -1); Fill(closeText.rectTransform); closeText.alignment = TextAlignmentOptions.Center;
            root.AddComponent<StageShopPanel>().EditorSetup(theme, dim.gameObject, wallet, pending, message, rows, rowPrefab, closeBtn);
            var panelPrefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPath).GetComponent<StageShopPanel>();
            Object.DestroyImmediate(root);

            // ④ Hud의 패널 호스트에 연결
            var hudPath = "Assets/_Project/Prefabs/UI/Hud.prefab";
            var hud = PrefabUtility.LoadPrefabContents(hudPath);
            var host = hud.GetComponentInChildren<WorldPanelHost>(true);
            host.EditorSetupStageShop(panelPrefab);
            PrefabUtility.SaveAsPrefabAsset(hud, hudPath);
            PrefabUtility.UnloadPrefabContents(hud);

            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Debug.Log("[RatGame] 목적지 상점 — 판매대·패널·Hud 연결");
        }

        private static UiThemeSO FindTheme()
        {
            var mirror = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MirrorPanel.prefab");
            foreach (var t in mirror.GetComponentsInChildren<ThemedGraphic>(true))
            {
                var theme = new SerializedObject(t).FindProperty("_theme").objectReferenceValue as UiThemeSO;
                if (theme != null) return theme;
            }
            return null;
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) { go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); }
            else go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        private static void Le(RectTransform rt, float w, float h)
        {
            var le = rt.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            if (w > 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h > 0) { le.preferredHeight = h; le.minHeight = h; }
        }

        private static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

        private static TextMeshProUGUI Tmp(string name, RectTransform parent, string text, UiThemeSO theme, UiColorRole color, UiTextRole role, float height)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.raycastTarget = false; t.alignment = TextAlignmentOptions.MidlineLeft;
            rt.gameObject.AddComponent<ThemedGraphic>().Setup(theme, color, role);
            if (height > 0) Le(rt, -1, height);
            return t;
        }
    }
}
