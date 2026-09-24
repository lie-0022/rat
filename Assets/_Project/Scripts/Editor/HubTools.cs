using RatGame.World;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>기지(Hub) 씬 배치 도구 (고양이 59). 다시 실행하면 지우고 새로 만든다.</summary>
    public static class HubTools
    {
        private const string HubScene = "Assets/_Project/Scenes/Hub.unity";

        [MenuItem("Tools/RatGame/Hub/Add Stage Board")]
        public static void AddStageBoard()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중엔 안 됨"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            var pad = Object.FindFirstObjectByType<DeparturePad>();
            if (pad == null) { Debug.LogError("Hub에 DeparturePad 없음"); return; }
            var old = GameObject.Find("StageBoard");
            if (old != null) Object.DestroyImmediate(old);

            // 동쪽 벽 앞, 출발 발판 옆 — 발판에 서기 전에 보인다
            var root = new GameObject("StageBoard");
            root.transform.position = new Vector3(5.2f, 0f, 3.5f);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.7f, 0f);
            box.size = new Vector3(0.4f, 1.4f, 1.2f);
            root.AddComponent<NetworkObject>();
            var board = root.AddComponent<StageBoard>();

            var frameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/CodexPedestal.mat");
            Cube(root.transform, "Leg", new Vector3(0f, 0.35f, 0f), new Vector3(0.1f, 0.7f, 0.1f), frameMat);
            Cube(root.transform, "Panel", new Vector3(0f, 1.05f, 0f), new Vector3(0.08f, 0.7f, 1.2f), frameMat);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(-0.06f, 1.05f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 방 안(서쪽)에서 읽힌다
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Jua/Jua SDF.asset");
            label.fontSize = 0.8f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(1.1f, 0.6f);
            label.text = "[E] 목적지";

            board.EditorSetup(pad, label);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HubTools] 목적지 게시판 배치 — Hub (5.2, 0, 3.5)");
        }

        private static void Cube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 판정은 루트 박스 하나
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
