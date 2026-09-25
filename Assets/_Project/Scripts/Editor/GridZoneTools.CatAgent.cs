using RatGame.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.Editor
{
    /// <summary>
    /// 큰 고양이용 NavMesh 에이전트 타입 "BigCat" (고양이 141 — 실제 비율). 런타임 CreateSettings는 반지름을 못 바꿔서
    /// 프로젝트 NavMesh 설정(ProjectSettings/NavMeshAreas)에 넣는다 — AI Navigation 샘플의 방식. 크기는 벽 속 테마 SO에서.
    /// </summary>
    public static partial class GridZoneTools
    {
        private const string BigCatAgentName = "BigCat";

        [MenuItem("Tools/RatGame/Zone/Create Big Cat Nav Agent")]
        public static void CreateBigCatAgent()
        {
            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var agents = so.FindProperty("m_Settings");
            var names = so.FindProperty("m_SettingNames");
            int index = -1;
            for (int i = 0; i < names.arraySize; i++) if (names.GetArrayElementAtIndex(i).stringValue == BigCatAgentName) index = i;
            if (index < 0)
            {
                var created = NavMesh.CreateSettings(); // 새 번호만 받는다
                names.InsertArrayElementAtIndex(names.arraySize);
                names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = BigCatAgentName;
                agents.InsertArrayElementAtIndex(agents.arraySize); // 마지막(휴머노이드) 값을 복사해 나머지 항목을 채움
                index = agents.arraySize - 1;
                agents.GetArrayElementAtIndex(index).FindPropertyRelative("agentTypeID").intValue = created.agentTypeID;
            }
            var e = agents.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("agentRadius").floatValue = zone.CatNavRadius;
            e.FindPropertyRelative("agentHeight").floatValue = zone.CatNavHeight;
            e.FindPropertyRelative("agentClimb").floatValue = 0.75f;
            e.FindPropertyRelative("agentSlope").floatValue = 45f;
            so.ApplyModifiedProperties();
            zone.CatAgentTypeId = e.FindPropertyRelative("agentTypeID").intValue;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RatGame] NavMesh 에이전트 '{BigCatAgentName}' id {zone.CatAgentTypeId}, 반지름 {zone.CatNavRadius}, 높이 {zone.CatNavHeight}");
        }
    }
}
