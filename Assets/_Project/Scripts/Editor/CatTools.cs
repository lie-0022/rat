using RatGame.AI;
using RatGame.Data;
using RatGame.World;
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

        /// <summary>
        /// Tools/RatGame/Cat/Build Demo Layout — Stage_Warehouse01에 스팟 6개·시야 가림 상자·고양이 1마리 배치 + NavMesh 베이크
        /// (design/cat-design/02-6 데모 배치안 1단계: 바닥 한 층, 선반·틈은 다음 단계). 이미 있는 오브젝트는 건너뛴다.
        /// </summary>
        [MenuItem("Tools/RatGame/Cat/Build Demo Layout (Stage_Warehouse01)")]
        public static void BuildDemoLayout()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Stage_Warehouse01") { Debug.LogWarning("[RatGame] Stage_Warehouse01을 연 뒤 실행"); return; }

            var root = GameObject.Find("CatLayout") ?? new GameObject("CatLayout");
            // (이름, 종류, 위치, 바라보는 방향, 가중치) — 쥐구멍(5,6)에서 먼 쪽에 잠자리·밥, 가까운 쪽은 관찰점
            var spots = new (string name, CatSpotType type, Vector3 pos, Vector3 look, float weight)[]
            {
                ("Spot_Bed",   CatSpotType.Bed,   new(-12f, 0f, 14f),  new(1f, 0f, -1f), 1f),
                ("Spot_Food",  CatSpotType.Food,  new(14f, 0f, 14f),   new(0f, 0f, 1f),  1.2f),
                ("Spot_Sun",   CatSpotType.Sun,   new(16f, 0f, -10f),  new(1f, 0f, 0f),  0.8f),
                ("Spot_Groom", CatSpotType.Groom, new(10f, 0f, -14f),  new(-1f, 0f, 1f), 0.8f),
                ("Spot_LookA", CatSpotType.Look,  new(0f, 0f, -6f),    new(0f, 0f, 1f),  1.5f),
                ("Spot_LookB", CatSpotType.Look,  new(-8f, 0f, 2f),    new(1f, 0f, 0f),  1.5f),
                ("Spot_DoorW", CatSpotType.Door,  new(-19f, 0f, 0f),   new(1f, 0f, 0f),  1f),   // 집주인이 부르면 나가는 문 (design/cat-ideas/10)
                ("Spot_DoorE", CatSpotType.Door,  new(19f, 0f, 6f),    new(-1f, 0f, 0f), 1f),
            };
            foreach (var (name, type, pos, look, weight) in spots)
            {
                var go = GameObject.Find(name);
                if (go == null) { go = new GameObject(name); go.transform.SetParent(root.transform); }
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(look.normalized);
                var spot = go.GetComponent<CatSpot>() ?? go.AddComponent<CatSpot>();
                spot.EditorSetup(type, weight);
            }

            // 시야를 가리는 상자 몇 개 (그레이박스) — 빈 40×40에서는 항상 보이기 때문
            var crates = new (string name, Vector3 pos, Vector3 size)[]
            {
                ("Crate_A", new(0f, 0.75f, 4f),   new(4f, 1.5f, 1.5f)),
                ("Crate_B", new(-6f, 0.75f, 9f),  new(1.5f, 1.5f, 5f)),
                ("Crate_C", new(8f, 0.75f, -2f),  new(1.5f, 1.5f, 6f)),
                ("Crate_D", new(4f, 0.75f, 12f),  new(5f, 1.5f, 1.5f)),
            };
            foreach (var (name, pos, size) in crates)
            {
                if (GameObject.Find(name) != null) continue;
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = name; c.transform.SetParent(root.transform);
                c.transform.position = pos; c.transform.localScale = size;
                c.isStatic = true;
            }

            // 숨을 곳 2개 (design/cat-ideas/14): 관찰점 A 근처 신발(1인), 상자 B 옆 빈 상자(2인). 트리거 콜라이더 — 안으로 순간이동하므로 몸이 끼지 않게
            var hideBalance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            var hides = new (string name, string label, int cap, Vector3 pos, Vector3 size, Vector3 look)[]
            {
                ("Hide_Shoe", "장화", 1, new(3f, 0.7f, -8f),  new(1.1f, 1.4f, 2.2f), new(-1f, 0f, 0f)),
                ("Hide_Box",  "빈 상자", 2, new(-3.5f, 0.75f, 7f), new(1.8f, 1.5f, 1.8f), new(1f, 0f, 0f)),
            };
            foreach (var (name, label, cap, pos, size, look) in hides)
            {
                if (GameObject.Find(name) != null) continue;
                var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
                h.name = name; h.transform.SetParent(root.transform);
                h.transform.position = pos; h.transform.localScale = size;
                h.transform.rotation = Quaternion.LookRotation(look);
                h.GetComponent<BoxCollider>().isTrigger = true;
                var r = h.GetComponent<Renderer>();
                var mat = new Material(r.sharedMaterial) { color = new Color(0.45f, 0.3f, 0.2f) };
                r.sharedMaterial = mat;
                h.AddComponent<Unity.Netcode.NetworkObject>();
                var hs = h.AddComponent<HideSpot>();
                hs.EditorSetup(hideBalance, label, cap);
            }

            if (GameObject.Find("Cat") == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Cat/Cat.prefab");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = "Cat";
                inst.transform.position = new Vector3(-12f, 0f, 14f); // 잠자리에서 시작
            }

            var ground = GameObject.Find("Ground");
            var surface = ground.GetComponent<NavMeshSurface>() ?? ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[RatGame] 고양이 데모 레이아웃: 스팟 6 + 문 2 · 상자 4 · 숨을 곳 2 · 고양이 1 · NavMesh 베이크");
        }
    }
}
