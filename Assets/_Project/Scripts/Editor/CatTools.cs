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
                ("Spot_Litter", CatSpotType.Litter, new(-15f, 0f, -15f), new(1f, 0f, 1f), 0.6f), // 화장실 → 우다다 (design/cat-ideas/02)
                ("Spot_Water",  CatSpotType.Water,  new(11f, 0f, 16f),   new(0f, 0f, 1f), 0.6f),
                ("Spot_Ambush", CatSpotType.Ambush, new(-7.6f, 0f, 9f), new(0f, 0f, -1f), 0.5f), // 상자 B 옆 매복 (design/cat-ideas/09)
                ("Spot_Box",    CatSpotType.Box,    new(-2.1f, 0f, 7f), new(1f, 0f, 0f),  0.5f), // 빈 상자(숨을 곳) 입구에 앉기
                ("Spot_Perch",  CatSpotType.Perch,  new(14f, 1.6f, 8.3f), new(0f, 0f, -1f), 0.8f), // 선반 위 관찰대 (고양이 39)
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

            // 선반 (고양이 39) — 판 윗면 y 1.6, 밑 1.4m는 쥐만(쥐 키 1.3, 고양이 에이전트 높이 1.7). 앞에 점프 링크
            if (GameObject.Find("Shelf_Perch") == null)
            {
                var shelf = new GameObject("Shelf_Perch");
                shelf.transform.SetParent(root.transform);
                shelf.transform.position = new Vector3(14f, 0f, 8f);
                var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                top.name = "Top"; top.transform.SetParent(shelf.transform, false);
                top.transform.localPosition = new Vector3(0f, 1.5f, 0f); top.transform.localScale = new Vector3(4f, 0.2f, 2.2f);
                top.isStatic = true;
                var topR = top.GetComponent<Renderer>();
                topR.sharedMaterial = new Material(topR.sharedMaterial) { color = new Color(0.55f, 0.4f, 0.28f) };
                foreach (var lp in new[] { new Vector3(-1.9f, 0.7f, -1f), new Vector3(1.9f, 0.7f, -1f), new Vector3(-1.9f, 0.7f, 1f), new Vector3(1.9f, 0.7f, 1f) })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leg.name = "Leg"; leg.transform.SetParent(shelf.transform, false);
                    leg.transform.localPosition = lp; leg.transform.localScale = new Vector3(0.12f, 1.4f, 0.12f);
                    leg.isStatic = true;
                }
                var link = shelf.AddComponent<NavMeshLink>();
                link.startPoint = new Vector3(0f, 0f, -2.2f);  // 바닥 (14, 0, 5.8)
                link.endPoint = new Vector3(0f, 1.6f, -0.4f);  // 윗면 (14, 1.6, 7.6)
                link.width = 1f;
                link.bidirectional = true;
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

            // 어둠 구역 (docs/07, 고양이 31) — 동쪽 8×8. 바닥의 어두운 판이 표시
            if (GameObject.Find("DarkZone_East") == null)
            {
                var dz = new GameObject("DarkZone_East");
                dz.transform.SetParent(root.transform);
                dz.transform.position = new Vector3(13f, 0f, -2f);
                dz.layer = LayerMask.NameToLayer("Ignore Raycast");
                var box = dz.AddComponent<BoxCollider>();
                box.isTrigger = true; box.center = new Vector3(0f, 1.5f, 0f); box.size = new Vector3(8f, 3f, 8f);
                var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mark.name = "DarkFloor"; mark.transform.SetParent(dz.transform, false);
                mark.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                mark.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                mark.transform.localScale = new Vector3(8f, 8f, 1f);
                Object.DestroyImmediate(mark.GetComponent<Collider>());
                var mr = mark.GetComponent<Renderer>();
                mr.sharedMaterial = new Material(mr.sharedMaterial) { color = new Color(0.04f, 0.04f, 0.1f) };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dz.AddComponent<Unity.Netcode.NetworkObject>();
                dz.AddComponent<LightZone>().EditorSetup(mr);
            }

            // TV (design/cat-ideas/10) — 남쪽 벽 앞, 화면은 북쪽(+z)을 본다
            if (GameObject.Find("TvSet") == null)
            {
                var tvGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tvGo.name = "TvSet"; tvGo.transform.SetParent(root.transform);
                tvGo.transform.position = new Vector3(8f, 0.75f, -19.2f);
                tvGo.transform.localScale = new Vector3(2.4f, 1.5f, 0.3f);
                tvGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                var tr = tvGo.GetComponent<Renderer>();
                tr.sharedMaterial = new Material(tr.sharedMaterial) { color = new Color(0.08f, 0.08f, 0.1f) };
                tvGo.AddComponent<Unity.Netcode.NetworkObject>();
                tvGo.AddComponent<TvSet>().EditorSetup(hideBalance);
            }

            // 로봇청소기 (design/cat-ideas/10) — 충전대 서쪽 벽
            if (GameObject.Find("RobotVacuum") == null)
            {
                var vac = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vac.name = "RobotVacuum"; vac.transform.SetParent(root.transform);
                vac.transform.position = new Vector3(-16f, 0.1f, -4f);
                vac.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
                var vr = vac.GetComponent<Renderer>();
                vr.sharedMaterial = new Material(vr.sharedMaterial) { color = new Color(0.15f, 0.15f, 0.18f) };
                var rb = vac.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
                vac.AddComponent<Unity.Netcode.NetworkObject>();
                vac.AddComponent<NetworkTransform>();
                vac.AddComponent<RobotVacuum>().EditorSetup(hideBalance);
            }

            // 물그릇 (design/cat-ideas/06 2단계) — 물 스팟 옆, 엎으면 웅덩이
            if (GameObject.Find("WaterBowl") == null)
            {
                var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bowl.name = "WaterBowl"; bowl.transform.SetParent(root.transform);
                bowl.transform.position = new Vector3(11.8f, 0.1f, 16f);
                bowl.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);
                var wr = bowl.GetComponent<Renderer>();
                wr.sharedMaterial = new Material(wr.sharedMaterial) { color = new Color(0.4f, 0.6f, 0.95f) };
                bowl.AddComponent<Unity.Netcode.NetworkObject>();
                bowl.AddComponent<WaterBowl>().EditorSetup(hideBalance);
            }

            // 흔들리는 끈 (design/cat-ideas/13, 고양이 38) — 서쪽, 기둥에 매단 장난감
            if (GameObject.Find("DanglingString") == null)
            {
                var ds = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ds.name = "DanglingString"; ds.transform.SetParent(root.transform);
                ds.transform.position = new Vector3(-12f, 0.9f, 4f);
                ds.transform.localScale = new Vector3(0.12f, 0.9f, 0.12f);
                var dsr = ds.GetComponent<Renderer>();
                dsr.sharedMaterial = new Material(dsr.sharedMaterial) { color = new Color(0.5f, 0.35f, 0.2f) };
                // 기둥 스케일을 안 물려받게 피벗은 루트 아래 별도 오브젝트
                var pivot = new GameObject("StringPivot").transform;
                pivot.SetParent(root.transform); pivot.position = new Vector3(-12f, 1.8f, 4.3f);
                var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cord.name = "Cord"; cord.transform.SetParent(pivot, false);
                cord.transform.localPosition = new Vector3(0f, -0.45f, 0f); cord.transform.localScale = new Vector3(0.02f, 0.45f, 0.02f);
                Object.DestroyImmediate(cord.GetComponent<Collider>());
                var toyBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                toyBall.name = "Toy"; toyBall.transform.SetParent(pivot, false);
                toyBall.transform.localPosition = new Vector3(0f, -0.95f, 0f); toyBall.transform.localScale = Vector3.one * 0.18f;
                Object.DestroyImmediate(toyBall.GetComponent<Collider>());
                var tr2 = toyBall.GetComponent<Renderer>();
                tr2.sharedMaterial = new Material(tr2.sharedMaterial) { color = new Color(0.95f, 0.4f, 0.6f) };
                ds.AddComponent<NetworkObject>();
                ds.AddComponent<DanglingString>().EditorSetup(hideBalance, pivot);
            }

            // 창문 (design/cat-ideas/06·10, 고양이 37) — 북쪽 벽, 바람은 남쪽(-z)으로
            if (GameObject.Find("WindowWind") == null)
            {
                var win = new GameObject("WindowWind");
                win.transform.SetParent(root.transform);
                win.transform.position = new Vector3(0f, 1.5f, 19.7f);
                win.transform.rotation = Quaternion.LookRotation(Vector3.back);
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "Frame"; frame.transform.SetParent(win.transform, false);
                frame.transform.localScale = new Vector3(2.2f, 1.6f, 0.1f);
                Object.DestroyImmediate(frame.GetComponent<Collider>());
                var fr = frame.GetComponent<Renderer>();
                fr.sharedMaterial = new Material(fr.sharedMaterial) { color = new Color(0.55f, 0.75f, 0.95f) };
                var hinge = new GameObject("PaneHinge").transform;
                hinge.SetParent(win.transform, false); hinge.localPosition = new Vector3(-1f, 0f, 0.08f);
                var pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pane.name = "Pane"; pane.transform.SetParent(hinge, false);
                pane.transform.localPosition = new Vector3(1f, 0f, 0f);
                pane.transform.localScale = new Vector3(2f, 1.4f, 0.05f);
                Object.DestroyImmediate(pane.GetComponent<Collider>());
                var pr2 = pane.GetComponent<Renderer>();
                pr2.sharedMaterial = new Material(pr2.sharedMaterial) { color = new Color(0.85f, 0.85f, 0.8f) };
                win.AddComponent<NetworkObject>();
                win.AddComponent<WindowWind>().EditorSetup(hideBalance, hinge);
            }

            // 후추통 (design/cat-ideas/06, 고양이 33) — 쥐구멍 쪽. 던지거나 세게 떨어뜨리면 쏟아진다
            if (GameObject.Find("PepperShaker") == null)
            {
                var pep = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pep.name = "PepperShaker"; pep.transform.SetParent(root.transform);
                pep.layer = LayerMask.NameToLayer("Carryable");
                pep.transform.position = new Vector3(-6f, 0.3f, -14f);
                pep.transform.localScale = new Vector3(0.18f, 0.2f, 0.18f);
                var pr = pep.GetComponent<Renderer>();
                pr.sharedMaterial = new Material(pr.sharedMaterial) { color = new Color(0.15f, 0.15f, 0.15f) };
                var prb = pep.AddComponent<Rigidbody>();
                prb.mass = 0.3f; prb.interpolation = RigidbodyInterpolation.Interpolate;
                pep.AddComponent<NetworkObject>();
                pep.AddComponent<NetworkTransform>();
                pep.AddComponent<NetworkRigidbody>();
                var grip = new GameObject("Grip_0").transform;
                grip.SetParent(pep.transform, false);
                grip.localPosition = Vector3.up * 0.5f;
                var item = pep.AddComponent<CarryableItem>();
                var iso = new SerializedObject(item);
                iso.FindProperty("_balance").objectReferenceValue = hideBalance;
                var gp = iso.FindProperty("_gripPoints");
                gp.arraySize = 1;
                gp.GetArrayElementAtIndex(0).objectReferenceValue = grip;
                iso.ApplyModifiedPropertiesWithoutUndo();
                pep.AddComponent<PepperShaker>().EditorSetup(hideBalance);
            }

            // 초인종 (design/cat-ideas/13) — 서쪽 문 옆, 런당 1회
            if (GameObject.Find("Doorbell") == null)
            {
                var bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bell.name = "Doorbell"; bell.transform.SetParent(root.transform);
                bell.transform.position = new Vector3(-18f, 0.4f, -2f);
                bell.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
                var br = bell.GetComponent<Renderer>();
                br.sharedMaterial = new Material(br.sharedMaterial) { color = new Color(0.9f, 0.8f, 0.2f) };
                bell.AddComponent<Unity.Netcode.NetworkObject>();
                bell.AddComponent<Doorbell>().EditorSetup(hideBalance);
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
            Debug.Log("[RatGame] 고양이 데모 레이아웃: 스팟 6 + 문 2 · 상자 4 · 숨을 곳 2 · 초인종 1 · 고양이 1 · NavMesh 베이크");
        }
    }
}
