using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// Tools/RatGame/Apply Basic Art — 그레이박스 구분용 기본 아트 패스.
    /// URP Lit 색 머티리얼 생성 + 프리팹/씬에 적용, 플레이어(귀·꼬리)·고양이(귀·꼬리) 형태 드레싱,
    /// 아이템별 실루엣 보정. 진짜 아트(엄지성 트랙)가 들어오면 교체된다 — 전부 재실행 안전.
    /// </summary>
    public static class ArtTools
    {
        private const string MatDir = "Assets/_Project/Art/Materials";

        // 전리품 색 (docs/08 20종)
        private static readonly Dictionary<string, Color> LootColors = new()
        {
            { "loot_cracker", new Color(0.87f, 0.72f, 0.45f) },
            { "loot_candy", new Color(1f, 0.45f, 0.7f) },
            { "loot_coin", new Color(1f, 0.84f, 0.2f) },
            { "loot_bottlecap", new Color(0.7f, 0.75f, 0.8f) },
            { "loot_grape", new Color(0.55f, 0.27f, 0.68f) },
            { "loot_cheese_bit", new Color(1f, 0.85f, 0.3f) },
            { "loot_egg", new Color(0.98f, 0.95f, 0.88f) },
            { "loot_jelly", new Color(0.5f, 0.95f, 0.4f) },
            { "loot_plate", new Color(0.9f, 0.94f, 1f) },
            { "loot_soap", new Color(0.4f, 0.85f, 0.8f) },
            { "loot_can", new Color(0.6f, 0.68f, 0.75f) },
            { "loot_lightbulb", new Color(1f, 0.95f, 0.7f) },
            { "loot_cheese_wheel", new Color(1f, 0.8f, 0.15f) },
            { "loot_baguette", new Color(0.82f, 0.62f, 0.35f) },
            { "loot_ham", new Color(0.95f, 0.5f, 0.5f) },
            { "loot_watermelon", new Color(0.3f, 0.7f, 0.3f) },
            { "loot_chicken", new Color(0.75f, 0.5f, 0.25f) },
            { "loot_ring", new Color(1f, 0.9f, 0.4f) },
            { "loot_phone", new Color(0.12f, 0.12f, 0.15f) },
            { "loot_watch", new Color(0.85f, 0.7f, 0.35f) },
        };

        // 회색박스: 가치 티어 색 코딩
        private static readonly Dictionary<string, Color> MiscColors = new()
        {
            { "GrayBox_S", new Color(0.95f, 0.9f, 0.5f) },
            { "GrayBox_M", new Color(0.55f, 0.85f, 0.5f) },
            { "GrayBox_L", new Color(0.45f, 0.65f, 0.95f) },
            { "GrayBox_XL", new Color(0.7f, 0.45f, 0.9f) },
            { "GrayBox_XXL", new Color(0.95f, 0.4f, 0.4f) },
            { "Test_Egg", new Color(0.98f, 0.95f, 0.88f) },
            { "Test_Plate", new Color(0.9f, 0.94f, 1f) },
            { "Test_Phone", new Color(0.12f, 0.12f, 0.15f) },
            { "Test_Jelly", new Color(0.5f, 0.95f, 0.4f) },
            { "Lure_Yarn", new Color(0.9f, 0.25f, 0.3f) },
            { "Lure_Catnip", new Color(0.35f, 0.75f, 0.35f) },
            { "Lure_Crumbs", new Color(0.6f, 0.45f, 0.28f) },
        };

        [MenuItem("Tools/RatGame/Apply Basic Art")]
        public static void ApplyBasicArt()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");

            // 1) 프리팹 색 적용
            foreach (var kv in LootColors)
                TintPrefab($"Assets/_Project/Prefabs/Items/Loot/{kv.Key}.prefab", kv.Value);
            foreach (var kv in MiscColors)
                TintPrefab($"Assets/_Project/Prefabs/Items/{kv.Key}.prefab", kv.Value);

            // 2) 실루엣 보정 (프리팹 비주얼만 — 콜라이더 유지)
            ReshapeLoot();

            // 3) 플레이어: 쥐 드레싱 (귀 2 + 꼬리) — 몸 색은 PlayerVisual이 클라별로 칠한다
            DressPlayer();

            // 4) 고양이: 주황 몸 + 귀 + 꼬리
            DressCat();

            // 5) 씬 환경 (Sandbox_Net + Hub)
            TintScene("Assets/_Project/Scenes/Sandbox_Net.unity");
            TintScene("Assets/_Project/Scenes/Hub.unity");

            AssetDatabase.SaveAssets();
            Debug.Log("[RatGame] 기본 아트 패스 적용 완료");
        }

        // ---- 유틸 ----

        private static Material GetMat(string name, Color color)
        {
            string path = $"{MatDir}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void TintPrefab(string path, Color color)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            var mat = GetMat(root.name, color);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ReshapeLoot()
        {
            // (프리팹, 비주얼 스케일 보정) — 실루엣만 다르게
            var shapes = new Dictionary<string, Vector3>
            {
                { "Assets/_Project/Prefabs/Items/Loot/loot_egg.prefab", new Vector3(0.85f, 1.15f, 0.85f) },
                { "Assets/_Project/Prefabs/Items/Loot/loot_plate.prefab", new Vector3(1.3f, 0.15f, 1.3f) },
                { "Assets/_Project/Prefabs/Items/Loot/loot_phone.prefab", new Vector3(0.55f, 0.12f, 1f) },
                { "Assets/_Project/Prefabs/Items/Loot/loot_baguette.prefab", new Vector3(0.35f, 0.35f, 1.6f) },
                { "Assets/_Project/Prefabs/Items/Loot/loot_watch.prefab", new Vector3(1f, 0.3f, 1f) },
                { "Assets/_Project/Prefabs/Items/Loot/loot_ring.prefab", new Vector3(1f, 0.4f, 1f) },
            };
            foreach (var kv in shapes)
            {
                var root = PrefabUtility.LoadPrefabContents(kv.Key);
                Vector3 baseScale = root.transform.localScale;
                root.transform.localScale = Vector3.Scale(
                    new Vector3(Mathf.Abs(baseScale.x), Mathf.Abs(baseScale.x), Mathf.Abs(baseScale.x)), kv.Value);
                PrefabUtility.SaveAsPrefabAsset(root, kv.Key);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DressPlayer()
        {
            string path = "Assets/_Project/Prefabs/Player/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var bodyMat = GetMat("PlayerBody", new Color(0.75f, 0.75f, 0.78f));
            var pinkMat = GetMat("RatPink", new Color(1f, 0.72f, 0.78f));
            root.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            if (root.transform.Find("Ear_L") == null)
            {
                AddPart(root.transform, "Ear_L", PrimitiveType.Sphere, new Vector3(-0.22f, 0.92f, 0.1f), Vector3.one * 0.3f, pinkMat);
                AddPart(root.transform, "Ear_R", PrimitiveType.Sphere, new Vector3(0.22f, 0.92f, 0.1f), Vector3.one * 0.3f, pinkMat);
                AddPart(root.transform, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0.35f, 0.5f), Vector3.one * 0.16f, pinkMat);
                var tail = AddPart(root.transform, "Tail", PrimitiveType.Capsule, new Vector3(0f, -0.55f, -0.55f), new Vector3(0.1f, 0.45f, 0.1f), pinkMat);
                tail.localRotation = Quaternion.Euler(120f, 0f, 0f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void DressCat()
        {
            string path = "Assets/_Project/Prefabs/Cat/Cat.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var catMat = GetMat("CatBody", new Color(0.95f, 0.55f, 0.2f));
            var darkMat = GetMat("CatDark", new Color(0.6f, 0.32f, 0.1f));
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = catMat;

            var visual = root.transform.Find("Visual");
            if (visual != null && visual.Find("Ear_L") == null)
            {
                AddPart(visual, "Ear_L", PrimitiveType.Cube, new Vector3(-0.28f, 1.05f, 0.05f), new Vector3(0.25f, 0.35f, 0.12f), darkMat);
                AddPart(visual, "Ear_R", PrimitiveType.Cube, new Vector3(0.28f, 1.05f, 0.05f), new Vector3(0.25f, 0.35f, 0.12f), darkMat);
                var tail = AddPart(visual, "Tail", PrimitiveType.Capsule, new Vector3(0f, 0.2f, -0.85f), new Vector3(0.18f, 0.6f, 0.18f), darkMat);
                tail.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                AddPart(visual, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.35f, 0.62f), Vector3.one * 0.35f, darkMat);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Transform AddPart(Transform parent, string name, PrimitiveType prim, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(prim);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 비주얼 전용
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static void TintScene(string scenePath)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var groundMat = GetMat("Ground", new Color(0.85f, 0.78f, 0.65f));   // 부엌 바닥 느낌
            var wallMat = GetMat("Wall", new Color(0.55f, 0.48f, 0.42f));
            var holeMat = GetMat("RatHole", new Color(0.08f, 0.06f, 0.05f));
            var listenerMat = GetMat("Listener", new Color(0.9f, 0.6f, 0.9f));

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string n = renderer.gameObject.name;
                if (n == "Ground") renderer.sharedMaterial = groundMat;
                else if (n.StartsWith("Wall_") || n == "NoiseWall") renderer.sharedMaterial = wallMat;
                else if (n == "RatHole") renderer.sharedMaterial = holeMat;
                else if (n.StartsWith("Listener_")) renderer.sharedMaterial = listenerMat;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
