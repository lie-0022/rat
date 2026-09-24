using RatGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>스테이지 사이 상점 (plan cat-65): 창고 데모 씬의 레이저·후추를 네트워크 프리팹으로 뽑고, 상점 목록 에셋을 만들어 벽 속 테마에 연결.</summary>
    public static partial class GridZoneTools
    {
        private const string ShopItemDir = "Assets/_Project/Prefabs/Items";
        private const string ShopPath = "Assets/_Project/Data/Zones/StageShop_Walls.asset";

        [MenuItem("Tools/RatGame/Zone/Create Stage Shop Items")]
        public static void CreateStageShopItems()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중엔 안 됨"); return; }
            string returnTo = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Stage_Warehouse01.unity", OpenSceneMode.Single);
            var laser = ExtractPrefab("LaserPointer", $"{ShopItemDir}/Shop_LaserPointer.prefab");
            var pepper = ExtractPrefab("PepperShaker", $"{ShopItemDir}/Shop_PepperShaker.prefab");
            var yarn = AssetDatabase.LoadAssetAtPath<GameObject>($"{ShopItemDir}/Lure_Yarn.prefab");

            var shop = AssetDatabase.LoadAssetAtPath<StageShopSO>(ShopPath);
            if (shop == null) { shop = ScriptableObject.CreateInstance<StageShopSO>(); AssetDatabase.CreateAsset(shop, ShopPath); }
            shop.Entries = new[]
            {
                new StageShopEntry { Id = "yarn", DisplayName = "털실", Prefab = yarn, Price = 40, Description = "던지면 고양이가 쫓아가요" },
                new StageShopEntry { Id = "pepper", DisplayName = "후추통", Prefab = pepper, Price = 50, Description = "쏟으면 냄새 자국이 지워지고 고양이가 재채기" },
                new StageShopEntry { Id = "laser", DisplayName = "레이저 포인터", Prefab = laser, Price = 80, Description = "들고 있으면 빨간 점 — 한가한 고양이가 쫓아요" },
            };
            EditorUtility.SetDirty(shop);
            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            zone.Shop = shop;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log($"[RatGame] 상점 물건 3종 → {ShopPath}");
        }

        // 씬 오브젝트를 복사해 프리팹으로 (씬에만 있던 머티리얼은 에셋으로 바꿔서 — 고양이 58의 마젠타 교훈)
        private static GameObject ExtractPrefab(string sceneName, string path)
        {
            var src = GameObject.Find(sceneName);
            if (src == null) { Debug.LogWarning($"[RatGame] {sceneName} 없음"); return AssetDatabase.LoadAssetAtPath<GameObject>(path); }
            var copy = Object.Instantiate(src);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            ZoneTools.PersistMaterials(copy);
            copy.transform.SetParent(null);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            return prefab;
        }
    }
}
