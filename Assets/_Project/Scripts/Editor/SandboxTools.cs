using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// Tools/RatGame/Create Sandbox Boxes — docs/03 샌드박스 회색 박스 5종(mass 0.5/2/5/10/20)의
    /// LootItemSO + 프리팹을 자동 생성하고 Sandbox_Net 열려 있으면 씬에 배치까지 한다 (docs/14 자동화 원칙).
    /// </summary>
    public static class SandboxTools
    {
        // (이름, mass, 가치, 스케일, grip 수) — docs/05 무게 기준표의 회색 박스 대응
        private static readonly (string name, float mass, int value, float scale, int grips)[] Boxes =
        {
            ("GrayBox_S",   0.5f,  10, 0.4f, 1),
            ("GrayBox_M",   2f,    20, 0.6f, 1),
            ("GrayBox_L",   5f,    50, 0.9f, 2),
            ("GrayBox_XL",  10f,  100, 1.2f, 2),
            ("GrayBox_XXL", 20f,  200, 1.5f, 4),
        };

        [MenuItem("Tools/RatGame/Create Sandbox Boxes")]
        public static void CreateSandboxBoxes()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");

            foreach (var box in Boxes)
            {
                // 1) LootItemSO
                string soPath = $"Assets/_Project/Data/Items/{box.name}.asset";
                var so = AssetDatabase.LoadAssetAtPath<LootItemSO>(soPath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<LootItemSO>();
                    AssetDatabase.CreateAsset(so, soPath);
                }
                var serialized = new SerializedObject(so);
                serialized.FindProperty("_displayName").stringValue = box.name;
                serialized.FindProperty("_baseValue").intValue = box.value;
                serialized.FindProperty("_mass").floatValue = box.mass;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // 2) 프리팹
                string prefabPath = $"Assets/_Project/Prefabs/Items/{box.name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = box.name;
                    go.layer = LayerMask.NameToLayer("Carryable");
                    go.transform.localScale = Vector3.one * box.scale;

                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = box.mass;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    go.AddComponent<NetworkObject>();
                    go.AddComponent<NetworkTransform>();   // 서버 권위 + 보간 (docs/03)
                    go.AddComponent<NetworkRigidbody>();

                    // GripPoints: 윗면 모서리 쪽에 grip 수만큼
                    var grips = new Transform[box.grips];
                    for (int i = 0; i < box.grips; i++)
                    {
                        var grip = new GameObject($"Grip_{i}").transform;
                        grip.SetParent(go.transform, false);
                        float angle = (360f / box.grips) * i * Mathf.Deg2Rad;
                        grip.localPosition = new Vector3(Mathf.Cos(angle) * 0.5f, 0.5f, Mathf.Sin(angle) * 0.5f);
                        grips[i] = grip;
                    }

                    var item = go.AddComponent<CarryableItem>();
                    var itemSo = new SerializedObject(item);
                    itemSo.FindProperty("_data").objectReferenceValue = so;
                    itemSo.FindProperty("_balance").objectReferenceValue = balance;
                    var gripsProp = itemSo.FindProperty("_gripPoints");
                    gripsProp.arraySize = grips.Length;
                    for (int i = 0; i < grips.Length; i++)
                        gripsProp.GetArrayElementAtIndex(i).objectReferenceValue = grips[i];
                    itemSo.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    Object.DestroyImmediate(go);
                }
            }
            AssetDatabase.SaveAssets();

            // 3) Sandbox_Net이 열려 있으면 씬 배치 (박스 5개 + 쥐구멍)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name == "Sandbox_Net")
            {
                for (int i = 0; i < Boxes.Length; i++)
                {
                    string instName = Boxes[i].name;
                    if (GameObject.Find(instName) != null) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Items/{instName}.prefab");
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.position = new Vector3(-6f + i * 3f, 1f, 6f);
                }
                if (GameObject.Find("RatHole") == null)
                {
                    var hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    hole.name = "RatHole";
                    hole.tag = "RatHole";
                    hole.transform.position = new Vector3(0f, 0.5f, -18f);
                    hole.transform.localScale = new Vector3(3f, 1f, 2f);
                    hole.GetComponent<BoxCollider>().isTrigger = true;
                    hole.AddComponent<NetworkObject>();
                    hole.AddComponent<DepositZone>();
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[RatGame] 샌드박스 박스 5종 + 쥐구멍 생성 완료");
        }
    }
}
