using RatGame.UI;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>UiTheme 에셋을 바꾼 뒤 UI 프리팹 전체에 다시 입힌다 (docs/12 공통 스타일).</summary>
    public static class UiThemeTool
    {
        private const string UiPrefabFolder = "Assets/_Project/Prefabs/UI";

        [MenuItem("Tools/RatGame/UI/Apply Theme")]
        public static void ApplyThemeToUiPrefabs()
        {
            int prefabs = 0, elements = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var themed = root.GetComponentsInChildren<ThemedGraphic>(true);
                if (themed.Length > 0)
                {
                    foreach (var t in themed) t.Apply();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                    elements += themed.Length;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[Rat] UI 테마 적용: 프리팹 {prefabs}개, 요소 {elements}개");
        }
    }
}
