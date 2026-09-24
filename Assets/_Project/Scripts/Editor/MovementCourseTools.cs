using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 이동 검증 그레이박스 코스 (docs/04 수용 기준 "경사·단차·좁은 틈 완주", 고양이 136).
    /// +z로 곧게: 경사 15°·25°·35°(각 1m 올라 평지 2m → 낙하), 단차 0.1·0.2·0.3m, 웅크림 터널(천장 0.9m — 서면 1.2m, 웅크리면 0.6m).
    /// 빌드 목록엔 안 넣는다. 아무 씬 Play = DevSceneAutoBoot가 호스트까지.
    /// </summary>
    public static class MovementCourseTools
    {
        public const string ScenePath = "Assets/_Project/Scenes/Test_MovementCourse.unity";
        private const float Width = 4f;

        // 구간 표 (시작 z, 통과 판정 z) — 자동 시험이 같은 값을 쓴다. 경사 끝 = 시작 + h/tanθ + 평지 2m + 0.5
        public static readonly (string name, float start, float pass)[] Sections =
        {
            ("경사 15°", 3f, 9.3f), ("경사 25°", 13f, 17.7f), ("경사 35°", 21f, 25f),
            ("단차 0.1m", 28f, 30f), ("단차 0.2m", 32f, 34f), ("단차 0.3m", 36f, 38f),
            ("웅크림 터널", 41f, 44.5f),
        };

        [MenuItem("Tools/RatGame/Test/Create Movement Course")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("MovementCourse").transform;

            Box(root, "Floor", new Vector3(0f, -0.25f, 24f), new Vector3(Width + 2f, 0.5f, 52f), Quaternion.identity);
            Box(root, "RailL", new Vector3(-Width / 2f - 0.5f, 1f, 24f), new Vector3(0.2f, 2f, 52f), Quaternion.identity);
            Box(root, "RailR", new Vector3(Width / 2f + 0.5f, 1f, 24f), new Vector3(0.2f, 2f, 52f), Quaternion.identity);

            Ramp(root, "Ramp15", 3f, 15f, 1f);
            Ramp(root, "Ramp25", 13f, 25f, 1f);
            Ramp(root, "Ramp35", 21f, 35f, 1f);
            Step(root, "Step10", 28f, 0.1f);
            Step(root, "Step20", 32f, 0.2f);
            Step(root, "Step30", 36f, 0.3f);
            // 웅크림 터널: 천장 밑면 0.9m, 폭 전체 — 옆으로 돌아갈 수 없게
            Box(root, "TunnelCeiling", new Vector3(0f, 0.9f + 0.25f, 42.5f), new Vector3(Width + 1f, 0.5f, 3f), Quaternion.identity);
            Box(root, "TunnelWallAbove", new Vector3(0f, 1.9f, 41.05f), new Vector3(Width + 1f, 1.5f, 0.1f), Quaternion.identity);

            var spawn = new GameObject("PlayerSpawn_0");
            spawn.tag = "PlayerSpawn";
            spawn.transform.SetParent(root, false);
            spawn.transform.position = new Vector3(0f, 0.65f, 0.5f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[MovementCourse] {ScenePath} 생성 — 구간 {Sections.Length}개");
        }

        // 위 면이 (z0, 바닥) → (z0 + h/tanθ, h)를 잇는 경사 + 꼭대기 평지 2m (그 뒤는 낙하)
        private static void Ramp(Transform root, string name, float z0, float degrees, float h)
        {
            float a = degrees * Mathf.Deg2Rad, t = 0.3f;
            float run = h / Mathf.Tan(a), len = h / Mathf.Sin(a);
            var rot = Quaternion.Euler(-degrees, 0f, 0f);
            Vector3 up = rot * Vector3.up;
            Vector3 topMid = new(0f, h / 2f, z0 + run / 2f);
            Box(root, name, topMid - up * (t / 2f), new Vector3(Width, t, len), rot);
            Box(root, name + "_Top", new Vector3(0f, h / 2f, z0 + run + 1f), new Vector3(Width, h, 2f), Quaternion.identity);
        }

        private static void Step(Transform root, string name, float z0, float h) =>
            Box(root, name, new Vector3(0f, h / 2f, z0 + 0.75f), new Vector3(Width, h, 1.5f), Quaternion.identity);

        private static void Box(Transform root, string name, Vector3 pos, Vector3 size, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = size;
            go.isStatic = true;
        }
    }
}
