using RatGame.AI;
using RatGame.Data;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.Editor
{
    /// <summary>
    /// Tools/RatGame/Create Sandbox Cat — 고양이 프리팹 + 웨이포인트 3개 + NavMesh 베이크 (docs/07).
    /// 방 모듈 단계(2-1)에서는 ZoneGenerator가 런타임 베이크로 대체한다.
    /// </summary>
    public static class CatTools
    {
        [MenuItem("Tools/RatGame/Create Sandbox Cat")]
        public static void CreateSandboxCat()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");

            // 1) 프리팹
            string prefabPath = "Assets/_Project/Prefabs/Cat/Cat.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var root = new GameObject("Cat");
                root.layer = LayerMask.NameToLayer("Cat");

                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.5f, 0f);
                col.height = 1f;
                col.radius = 0.35f;

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1f;
                agent.angularSpeed = 360f;
                agent.acceleration = 12f;

                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                visual.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);

                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>(); // 호스트 권한 보간 (docs/07)
                root.AddComponent<CatMovement>();
                var senses = root.AddComponent<CatSenses>();
                var brain = root.AddComponent<CatBrain>();
                new SerializedObject(senses).FindProperty("_balance").objectReferenceValue = balance;
                var soS = new SerializedObject(senses); soS.FindProperty("_balance").objectReferenceValue = balance; soS.ApplyModifiedPropertiesWithoutUndo();
                var soB = new SerializedObject(brain); soB.FindProperty("_balance").objectReferenceValue = balance; soB.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
            }

            // 2) 씬 배치 (Sandbox_Net 한정)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name == "Sandbox_Net")
            {
                for (int i = 0; i < 3; i++)
                {
                    string wpName = $"CatWaypoint_{i}";
                    if (GameObject.Find(wpName) == null)
                    {
                        var wp = new GameObject(wpName);
                        wp.transform.position = new Vector3[] {
                            new(12f, 0f, -12f), new(12f, 0f, 12f), new(-12f, 0f, -12f) }[i];
                    }
                }
                if (GameObject.Find("Cat") == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.position = new Vector3(12f, 0f, -12f);
                }

                // 3) NavMesh 베이크 (Ground에 NavMeshSurface)
                var ground = GameObject.Find("Ground");
                var surface = ground.GetComponent<NavMeshSurface>();
                if (surface == null) surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.BuildNavMesh();

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[RatGame] 고양이 + 웨이포인트 + NavMesh 완료");
        }
    }
}
