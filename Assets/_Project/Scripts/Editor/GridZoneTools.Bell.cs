using System.Collections.Generic;
using RatGame.Data;
using RatGame.World;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>고양이 방울 (plan cat-140): 털실 프리팹을 본떠 방울 물건을 만들고(CatLure → CatBell, 금색), 벽 속 상점 목록에 "bell"을 더한다.</summary>
    public static partial class GridZoneTools
    {
        private const string BellPrefabPath = ShopItemDir + "/Shop_CatBell.prefab";
        private const string BellMaterialPath = "Assets/_Project/Art/Materials/CatBell.mat";

        [MenuItem("Tools/RatGame/Zone/Create Cat Bell")]
        public static void CreateCatBell()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중엔 안 됨"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BellMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.15f));
                mat.SetFloat("_Metallic", 0.8f);
                mat.SetFloat("_Smoothness", 0.7f);
                AssetDatabase.CreateAsset(mat, BellMaterialPath);
            }
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");

            // 격리된 프리팹 편집 씬에서 — 열린 씬을 더럽히지 않게
            var go = PrefabUtility.LoadPrefabContents(ShopItemDir + "/Lure_Yarn.prefab");
            go.name = "Shop_CatBell";
            Object.DestroyImmediate(go.GetComponent<CatLure>());
            go.AddComponent<CatBell>().EditorSetup(balance);
            go.transform.localScale = Vector3.one * 0.22f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, BellPrefabPath);
            PrefabUtility.UnloadPrefabContents(go);

            var shop = AssetDatabase.LoadAssetAtPath<StageShopSO>(ShopPath);
            var entries = new List<StageShopEntry>(shop.Entries);
            entries.RemoveAll(e => e.Id == "bell");
            entries.Add(new StageShopEntry { Id = "bell", DisplayName = "방울", Prefab = prefab, Price = 60, Description = "고양이에 던져 맞히면 지도(Tab)에 보여요" });
            shop.Entries = entries.ToArray();
            EditorUtility.SetDirty(shop);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RatGame] 고양이 방울 → {BellPrefabPath}, 상점 {shop.Entries.Length}종");
        }
    }
}
