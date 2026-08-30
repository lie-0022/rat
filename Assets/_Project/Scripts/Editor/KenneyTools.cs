using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// Tools/RatGame/Apply Kenney Models — Kenney Food Kit(CC0) 모델을 전리품 프리팹 비주얼로 교체하고
    /// Furniture Kit(CC0) 가구를 샌드박스에 배치(시야 차단물). 콜라이더는 기존 프리미티브 유지 —
    /// 물리 코미디와 판정 단순성 보존 (docs/00 필러 1). 재실행 안전.
    /// </summary>
    public static class KenneyTools
    {
        private const string FoodDir = "Assets/_Project/Art/Models/Kenney/Food";
        private const string FurnDir = "Assets/_Project/Art/Models/Kenney/Furniture";

        // loot id → 푸드킷 모델 (없는 것은 색 프리미티브 유지)
        private static readonly Dictionary<string, string> FoodMap = new()
        {
            { "loot_cracker", "cookie" },
            { "loot_candy", "lollypop" },
            { "loot_grape", "grapes" },
            { "loot_cheese_bit", "cheese-cut" },
            { "loot_egg", "egg" },
            { "loot_jelly", "pudding" },
            { "loot_plate", "plate-dinner" },
            { "loot_can", "can" },
            { "loot_cheese_wheel", "cheese" },
            { "loot_baguette", "loaf-baguette" },
            { "loot_ham", "whole-ham" },
            { "loot_watermelon", "watermelon" },
            { "loot_chicken", "turkey" },
        };

        [MenuItem("Tools/RatGame/Apply Kenney Models")]
        public static void ApplyKenneyModels()
        {
            foreach (var kv in FoodMap)
                ReplaceVisual($"Assets/_Project/Prefabs/Items/Loot/{kv.Key}.prefab", $"{FoodDir}/{kv.Value}.fbx");

            PlaceFurnitureInSandbox();
            AssetDatabase.SaveAssets();
            Debug.Log("[RatGame] Kenney 모델 적용 완료");
        }

        private static void ReplaceVisual(string prefabPath, string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Debug.LogWarning($"모델 없음: {modelPath}"); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            // 루트 프리미티브 렌더러 제거 (콜라이더는 유지)
            var mf = root.GetComponent<MeshFilter>();
            var mr = root.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
            if (mf != null) Object.DestroyImmediate(mf);

            var old = root.transform.Find("Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";
            foreach (var col in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col); // 비주얼 전용

            // 프리미티브 루트 스케일 기준으로 맞춤 (프리팹 편집 공간에선 Renderer/Collider.bounds가 0 — 메시로 계산)
            Vector3 rootScale = root.transform.localScale;
            float targetMax = Mathf.Max(rootScale.x, rootScale.y, rootScale.z); // 큐브/스피어 기준 크기 1
            Bounds modelBounds = CalcMeshBounds(visual);
            float modelMax = Mathf.Max(modelBounds.size.x, modelBounds.size.y, modelBounds.size.z);
            if (modelMax < 0.0001f) { Debug.LogWarning($"모델 바운즈 0: {modelPath}"); modelMax = 1f; }
            float scale = targetMax / modelMax / Mathf.Max(rootScale.x, 0.0001f); // 부모 스케일 보정 (자식은 부모 스케일을 곱해 받음)
            visual.transform.localScale = Vector3.one * scale;

            // 모델 바닥이 콜라이더 중심(원점) 근처 오게 — 시각 중심 정렬
            Bounds scaled = CalcMeshBounds(visual);
            visual.transform.localPosition = -scaled.center;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Bounds CalcBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.001f);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>프리팹 편집 공간용 — 메시 바운즈를 직접 변환해 부모 로컬 기준 바운즈 계산.</summary>
        private static Bounds CalcMeshBounds(GameObject go)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>();
            bool first = true;
            Bounds result = default;
            foreach (var f in filters)
            {
                if (f.sharedMesh == null) continue;
                Bounds mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 world = go.transform.parent.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (first) { result = new Bounds(world, Vector3.zero); first = false; }
                    else result.Encapsulate(world);
                }
            }
            return first ? new Bounds(Vector3.zero, Vector3.zero) : result;
        }

        // (모델명, 위치, Y회전, 스케일) — 쥐 스케일에 맞춰 가구는 크게
        private static readonly (string model, Vector3 pos, float yaw, float scale)[] Furniture =
        {
            ("table",          new Vector3(7f, 0f, -4f),   0f,  1.6f),
            ("chair",          new Vector3(5f, 0f, -5.5f), 30f, 1.6f),
            ("chair",          new Vector3(9f, 0f, -2.5f), 200f, 1.6f),
            ("kitchenFridge",  new Vector3(-16f, 0f, 9f),  90f, 1.8f),
            ("kitchenCabinet", new Vector3(-16f, 0f, 4f),  90f, 1.8f),
            ("loungeSofa",     new Vector3(16f, 0f, 3f),  -90f, 1.6f),
            ("cardboardBoxOpen", new Vector3(-9f, 0f, -14f), 15f, 1.5f),
        };

        private static void PlaceFurnitureInSandbox()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Sandbox_Net") return;

            var parent = GameObject.Find("Furniture") ?? new GameObject("Furniture");
            int i = 0;
            foreach (var f in Furniture)
            {
                string instName = $"Furn_{f.model}_{i++}";
                if (GameObject.Find(instName) != null) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnDir}/{f.model}.fbx");
                if (model == null) { Debug.LogWarning($"가구 없음: {f.model}"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, parent.transform);
                inst.name = instName;
                inst.transform.SetPositionAndRotation(f.pos, Quaternion.Euler(0f, f.yaw, 0f));
                inst.transform.localScale = Vector3.one * f.scale;

                // 시야 차단 + 충돌: 바운즈 박스 콜라이더, RoomStatic
                var bounds = CalcBounds(inst);
                var col = inst.AddComponent<BoxCollider>();
                col.center = inst.transform.InverseTransformPoint(bounds.center);
                Vector3 size = bounds.size / f.scale;
                col.size = size;
                SetLayerRecursive(inst, LayerMask.NameToLayer("RoomStatic"));
            }

            // 가구가 생겼으니 NavMesh 재베이크 (고양이 경로)
            var ground = GameObject.Find("Ground");
            var surface = ground != null ? ground.GetComponent<NavMeshSurface>() : null;
            if (surface != null) surface.BuildNavMesh();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
