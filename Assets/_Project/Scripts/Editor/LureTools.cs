using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>Tools/RatGame/Create Sandbox Lures — 유인 3종 프리팹 + 샌드박스 배치 (docs/07·08).</summary>
    public static class LureTools
    {
        // (이름, 종류, 프리미티브, 스케일, mass, 잡을 수 있나)
        private static readonly (string name, LureKind kind, PrimitiveType prim, float scale, float mass, bool carryable)[] Lures =
        {
            ("Lure_Yarn",   LureKind.Yarn,   PrimitiveType.Sphere,   0.35f, 0.5f, true),
            ("Lure_Catnip", LureKind.Catnip, PrimitiveType.Cylinder, 0.4f,  0.3f, false),
            ("Lure_Crumbs", LureKind.Crumbs, PrimitiveType.Cube,     0.25f, 0.1f, false),
        };

        [MenuItem("Tools/RatGame/Create Sandbox Lures")]
        public static void CreateSandboxLures()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");

            foreach (var lure in Lures)
            {
                string prefabPath = $"Assets/_Project/Prefabs/Items/{lure.name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;

                var go = GameObject.CreatePrimitive(lure.prim);
                go.name = lure.name;
                go.layer = LayerMask.NameToLayer("Carryable");
                go.transform.localScale = Vector3.one * lure.scale;

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = lure.mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                if (!lure.carryable) rb.isKinematic = true; // 설치형은 고정

                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();
                if (lure.carryable)
                {
                    go.AddComponent<NetworkRigidbody>();
                    var grip = new GameObject("Grip_0").transform;
                    grip.SetParent(go.transform, false);
                    grip.localPosition = Vector3.up * 0.5f;
                    var item = go.AddComponent<CarryableItem>();
                    var iso = new SerializedObject(item);
                    iso.FindProperty("_balance").objectReferenceValue = balance;
                    var gp = iso.FindProperty("_gripPoints");
                    gp.arraySize = 1;
                    gp.GetArrayElementAtIndex(0).objectReferenceValue = grip;
                    iso.ApplyModifiedPropertiesWithoutUndo();
                }

                var lureComp = go.AddComponent<CatLure>();
                var lso = new SerializedObject(lureComp);
                lso.FindProperty("_balance").objectReferenceValue = balance;
                lso.FindProperty("_kind").enumValueIndex = (int)lure.kind;
                lso.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name == "Sandbox_Net")
            {
                Place("Lure_Yarn", new Vector3(-3f, 0.5f, 0f));
                Place("Lure_Catnip", new Vector3(11f, 0.3f, 11f));  // 순찰 웨이포인트(12,12) 근처 — 자연 발동 확인
                Place("Lure_Crumbs", new Vector3(0f, 0.2f, -10f));
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[RatGame] 유인 3종 생성 완료");
        }

        private static void Place(string name, Vector3 pos)
        {
            if (GameObject.Find(name) != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Items/{name}.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = pos;
        }
    }
}
