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
    public static partial class CatTools
    {
        // 데모 레이아웃의 소품·방·함정 (TV~초인종) — BuildDemoLayout이 300줄 넘어 나눔 (고양이 241). 이미 있는 건 건너뛴다
        private static void BuildDemoProps(GameObject root, BalanceConfigSO hideBalance)
        {
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

            // 창고방 (고양이 40) — 남동쪽 모서리 x 13~20, z -20~-14. 서쪽 벽에 문, 북쪽 벽에 쥐 구멍(폭 0.8·높이 1.4)
            if (GameObject.Find("Storeroom") == null)
            {
                var room = new GameObject("Storeroom");
                room.transform.SetParent(root.transform);
                int roomLayer = LayerMask.NameToLayer("NoiseBlocker"); // 벽 소음 감쇠(docs/06) — NoiseBlocker는 시야도 막는다
                System.Action<string, Vector3, Vector3> wall = (n, center, size) =>
                {
                    var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    w.name = n; w.transform.SetParent(room.transform);
                    w.transform.position = center; w.transform.localScale = size;
                    w.layer = roomLayer; w.isStatic = true;
                };
                // 서쪽 벽 x 13: z -20~-14, 문틈 z -17.9~-16.1 (1.8m — 고양이 반경 0.55×2를 빼고도 길이 남게)
                wall("Wall_StoreW1", new Vector3(13f, 1.25f, -18.95f), new Vector3(0.2f, 2.5f, 2.1f));
                wall("Wall_StoreW2", new Vector3(13f, 1.25f, -15.05f), new Vector3(0.2f, 2.5f, 2.1f));
                // 북쪽 벽 z -14: x 13~20, 쥐 구멍 x 16.6~17.4 (위 인방 1.4~2.5)
                wall("Wall_StoreN1", new Vector3(14.8f, 1.25f, -14f), new Vector3(3.6f, 2.5f, 0.2f));
                wall("Wall_StoreN2", new Vector3(18.7f, 1.25f, -14f), new Vector3(2.6f, 2.5f, 0.2f));
                wall("Wall_StoreNHole", new Vector3(17f, 1.95f, -14f), new Vector3(0.8f, 1.1f, 0.2f));

                var door = new GameObject("RoomDoor");
                door.transform.SetParent(room.transform);
                door.transform.position = new Vector3(13f, 0f, -17f);
                door.transform.rotation = Quaternion.LookRotation(Vector3.left); // forward = 방 밖(서쪽)
                var doorBox = door.AddComponent<BoxCollider>(); // 상호작용 대상 (문틀 트리거)
                doorBox.isTrigger = true; doorBox.center = new Vector3(0f, 1f, 0f); doorBox.size = new Vector3(1.8f, 2f, 0.6f); // 로컬 x = 월드 z (문이 서쪽을 본다)
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(door.transform, false); hinge.localPosition = new Vector3(-0.9f, 0f, 0f);
                var pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pane.name = "DoorPane"; pane.transform.SetParent(hinge, false);
                pane.transform.localPosition = new Vector3(0.9f, 1.05f, 0f); pane.transform.localScale = new Vector3(1.8f, 2.1f, 0.08f);
                pane.layer = roomLayer;
                var paneR = pane.GetComponent<Renderer>();
                paneR.sharedMaterial = new Material(paneR.sharedMaterial) { color = new Color(0.6f, 0.45f, 0.3f) };
                var obstacle = pane.AddComponent<NavMeshObstacle>();
                obstacle.carving = true; obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one; obstacle.enabled = false;
                var mod = door.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true; // 열림·닫힘은 런타임 carve로만
                door.AddComponent<NetworkObject>();
                door.AddComponent<RoomDoor>().EditorSetup(hideBalance, hinge, obstacle, new Vector3(16.5f, 0f, -17f), new Vector3(7f, 0f, 6f));
            }

            // 함정 3종 (docs/10, 고양이 49): 쥐덫 = 창고방 쥐 구멍 안쪽(좁은 통로), 끈끈이 = 치즈 근처, 전기선 = 창고방 문 앞
            if (GameObject.Find("Trap_MouseTrap") == null)
            {
                var mt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mt.name = "Trap_MouseTrap"; mt.transform.SetParent(root.transform);
                mt.transform.position = new Vector3(17f, 0.03f, -14.7f); mt.transform.localScale = new Vector3(0.7f, 0.06f, 0.5f);
                var mtr = mt.GetComponent<Renderer>(); mtr.sharedMaterial = new Material(mtr.sharedMaterial) { color = new Color(0.75f, 0.6f, 0.4f) };
                var box = mt.GetComponent<BoxCollider>(); box.size = new Vector3(1f, 1f, 1f);
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Bar"; Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.transform.SetParent(mt.transform, false); bar.transform.localPosition = new Vector3(0f, 3f, 0f); bar.transform.localScale = new Vector3(0.9f, 0.6f, 0.08f);
                mt.AddComponent<NetworkObject>();
                var trap = mt.AddComponent<MouseTrap>(); trap.EditorSetup(hideBalance); trap.EditorSetupBar(bar.transform);
            }
            if (GameObject.Find("Trap_GluePad") == null)
            {
                var gp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gp.name = "Trap_GluePad"; gp.transform.SetParent(root.transform);
                gp.transform.position = new Vector3(-9f, 0.01f, 6f); gp.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
                var gr = gp.GetComponent<Renderer>(); gr.sharedMaterial = new Material(gr.sharedMaterial) { color = new Color(0.9f, 0.85f, 0.35f) };
                gp.AddComponent<NetworkObject>();
                gp.AddComponent<GluePad>().EditorSetup(hideBalance);
            }
            if (GameObject.Find("Trap_WireShock") == null)
            {
                var ws = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ws.name = "Trap_WireShock"; ws.transform.SetParent(root.transform);
                ws.transform.position = new Vector3(12.2f, 0.03f, -17f); ws.transform.localScale = new Vector3(0.3f, 0.06f, 1.8f);
                var wr = ws.GetComponent<Renderer>();
                ws.AddComponent<NetworkObject>();
                var wire = ws.AddComponent<WireShock>(); wire.EditorSetup(hideBalance); wire.EditorSetupWire(wr);
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

            // 레이저 포인터 (design/cat-ideas/13, 고양이 41) — 들고 있으면 조준점에 빨간 점
            if (GameObject.Find("LaserPointer") == null)
            {
                var lp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                lp.name = "LaserPointer"; lp.transform.SetParent(root.transform);
                lp.layer = LayerMask.NameToLayer("Carryable");
                lp.transform.position = new Vector3(-8f, 0.3f, -6f);
                lp.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);
                var lr = lp.GetComponent<Renderer>();
                lr.sharedMaterial = new Material(lr.sharedMaterial) { color = new Color(0.2f, 0.2f, 0.25f) };
                var lrb = lp.AddComponent<Rigidbody>();
                lrb.mass = 0.2f; lrb.interpolation = RigidbodyInterpolation.Interpolate;
                lp.AddComponent<NetworkObject>();
                lp.AddComponent<NetworkTransform>();
                lp.AddComponent<NetworkRigidbody>();
                var lgrip = new GameObject("Grip_0").transform;
                lgrip.SetParent(lp.transform, false); lgrip.localPosition = Vector3.up * 0.5f;
                var litem = lp.AddComponent<CarryableItem>();
                var lso = new SerializedObject(litem);
                lso.FindProperty("_balance").objectReferenceValue = hideBalance;
                var lgp = lso.FindProperty("_gripPoints"); lgp.arraySize = 1; lgp.GetArrayElementAtIndex(0).objectReferenceValue = lgrip;
                lso.ApplyModifiedPropertiesWithoutUndo();
                lp.AddComponent<LaserPointer>().EditorSetup(hideBalance);
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
        }
    }
}
