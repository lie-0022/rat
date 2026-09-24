using System.Collections.Generic;
using RatGame.Data;
using RatGame.Run;
using RatGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>"벽 속" 스테이지 씬 (고양이 62): Stage_Generated를 복사해 ZoneBuilder 대신 GridZoneBuilder, 빌드 목록, 기지 목적지 게시판에 추가.</summary>
    public static partial class GridZoneTools
    {
        private const string WallStagePath = "Assets/_Project/Scenes/Stage_Walls.unity";
        private const string GeneratedStagePath = "Assets/_Project/Scenes/Stage_Generated.unity";

        [MenuItem("Tools/RatGame/Zone/Create Wall Stage")]
        public static void CreateWallStage()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중엔 안 됨"); return; }
            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            if (zone == null) { Debug.LogError("Zone_Walls 없음 — Create Wall Rooms 먼저"); return; }
            string returnTo = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WallStagePath) == null) AssetDatabase.CopyAsset(GeneratedStagePath, WallStagePath);
            var scene = EditorSceneManager.OpenScene(WallStagePath, OpenSceneMode.Single);
            var rm = GameObject.Find("RunManager");
            var old = rm.GetComponent<ZoneBuilder>();
            if (old != null) Object.DestroyImmediate(old);
            var builder = rm.GetComponent<GridZoneBuilder>();
            if (builder == null) builder = rm.AddComponent<GridZoneBuilder>();
            builder.EditorSetup(zone);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(x => x.path == WallStagePath)) { list.Add(new EditorBuildSettingsScene(WallStagePath, true)); EditorBuildSettings.scenes = list.ToArray(); }

            // 기지 목적지 게시판에 "벽 속(생성)" (씬에 저장된 목록이라 코드 기본값으론 안 들어간다)
            var hub = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Hub.unity", OpenSceneMode.Single);
            var pad = Object.FindFirstObjectByType<DeparturePad>();
            var so = new SerializedObject(pad);
            var scenes = so.FindProperty("_stageScenes"); var names = so.FindProperty("_stageNames");
            bool has = false;
            for (int i = 0; i < scenes.arraySize; i++) if (scenes.GetArrayElementAtIndex(i).stringValue == "Stage_Walls") has = true;
            if (!has)
            {
                scenes.InsertArrayElementAtIndex(scenes.arraySize); scenes.GetArrayElementAtIndex(scenes.arraySize - 1).stringValue = "Stage_Walls";
                names.InsertArrayElementAtIndex(names.arraySize); names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = "벽 속(생성)";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(hub);
                EditorSceneManager.SaveScene(hub);
            }
            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log($"[RatGame] 벽 속 스테이지 — {WallStagePath}, 빌드 목록·기지 게시판 등록");
        }
    }
}
