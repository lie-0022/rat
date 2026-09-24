using RatGame.Player;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 쥐 모델 적용 (2026-09-24, 드라이브 `개발/모델링 파일/mouse.fbx`). 다시 실행하면 새로 만든다.
    /// ① RatModel 프리팹: FBX에서 완성 몸(Cylinder)·수염만 켜고, 남은 작업용 조각(Cube 블록 머리·Cylinder.001 몸 껍데기)은 끈다.
    ///    키를 플레이어 캡슐(로컬 높이 2 = 월드 1.2m)에 맞추고 발을 캡슐 바닥에. 얼굴은 +Z(플레이어 정면).
    /// ② Player 프리팹: 그레이박스 캡슐 렌더러·귀·코·꼬리를 지우고 RatModel을 자식으로. 털 칸을 PlayerVisual에 연결.
    /// 콜라이더·물리는 그대로 — 보이는 것만 바뀐다.
    /// </summary>
    public static class RatModelTools
    {
        private const string Fbx = "Assets/_Project/Art/Models/Rat/mouse.fbx";
        private const string ModelPrefab = "Assets/_Project/Prefabs/Player/RatModel.prefab";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Player/Player.prefab";
        private static readonly string[] Leftovers = { "Cube", "Cylinder.001" };
        private const string BodyPart = "Cylinder";

        [MenuItem("Tools/RatGame/Player/Apply Rat Model")]
        public static void Apply()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (src == null) { Debug.LogError($"[RatModel] {Fbx} 없음"); return; }

            // ① 감싸는 프리팹
            var root = new GameObject("RatModel");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
            model.transform.SetParent(root.transform, false);
            foreach (Transform ch in model.transform)
                if (System.Array.IndexOf(Leftovers, ch.name) >= 0) ch.gameObject.SetActive(false);
            var body = model.transform.Find(BodyPart).GetComponent<MeshRenderer>();
            Bounds b = body.bounds;
            float s = 2f / b.size.y;                       // 캡슐 로컬 높이 2
            model.transform.localScale = Vector3.one * s;
            b = body.bounds;
            model.transform.localPosition = new Vector3(0f, -1f - b.min.y, 0f); // 발 = 캡슐 바닥(-1)
            // 털 칸 = 가장 큰 서브메시 (이름이 바뀌어도 안 틀리게)
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            int furIndex = 0; uint most = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) if (mesh.GetIndexCount(i) > most) { most = mesh.GetIndexCount(i); furIndex = i; }

            string furName = body.sharedMaterials[furIndex].name;
            var modelPrefab = PrefabUtility.SaveAsPrefabAsset(root, ModelPrefab);
            Object.DestroyImmediate(root);

            // ② Player 프리팹
            var player = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            foreach (var name in new[] { "RatModel", "Ear_L", "Ear_R", "Nose", "Tail" })
            {
                var t = player.transform.Find(name);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var capsuleRenderer = player.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) Object.DestroyImmediate(capsuleRenderer);
            var capsuleFilter = player.GetComponent<MeshFilter>();
            if (capsuleFilter != null) Object.DestroyImmediate(capsuleFilter);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, player.transform);
            inst.name = "RatModel";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            var furRenderer = inst.GetComponentInChildren<MeshRenderer>(false);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(false)) if (r.name == BodyPart) furRenderer = r;
            player.GetComponent<PlayerVisual>().EditorSetup(furRenderer, furIndex);

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(player);
            Debug.Log($"[RatModel] 적용 — 배율 {s:0.000}, 털 칸 {furIndex}({furName}), Player 프리팹 갱신");
        }
    }
}
