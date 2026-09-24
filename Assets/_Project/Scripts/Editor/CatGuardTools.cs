using RatGame.AI;
using RatGame.Data;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 문지기 성격 에셋 (고양이 82). 무작위 추첨 제외 — GridZoneBuilder가 문지기로 정한 고양이만 이 성격이 된다.
    /// 다시 실행하면 값만 덮어쓰고, 고양이 프리팹 성격 목록에 없으면 끝에 붙인다(순서 = 동기화 인덱스라 앞에 끼우지 않음).
    /// </summary>
    public static class CatGuardTools
    {
        private const string AssetPath = "Assets/_Project/Data/Cats/Personality_Guard.asset";
        private const string CatPrefab = "Assets/_Project/Prefabs/Cat/Cat.prefab";

        [MenuItem("Tools/RatGame/Cat/Create Guard Personality")]
        public static void Create()
        {
            var p = AssetDatabase.LoadAssetAtPath<CatPersonalitySO>(AssetPath);
            if (p == null) { p = ScriptableObject.CreateInstance<CatPersonalitySO>(); AssetDatabase.CreateAsset(p, AssetPath); }
            // 눈·귀가 조금 밝고 잠이 짧다 — 초소를 지키는 고양이. 색은 다른 성격과 안 겹치는 푸른 회색
            p.EditorSetup("문지기", new Color(0.49f, 0.61f, 0.72f), 1.15f, 1.1f, 1f, 0.5f, 0.3f, 2f, 1.5f, 0f);
            p.EditorSetupGuard(true);
            EditorUtility.SetDirty(p);

            var root = PrefabUtility.LoadPrefabContents(CatPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<CatBrain>());
                var arr = so.FindProperty("_personalities");
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == p) has = true;
                if (!has)
                {
                    arr.arraySize++;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = p;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, CatPrefab);
                }
                Debug.Log($"[CatGuard] 문지기 성격 — 프리팹 목록 {arr.arraySize}개 ({(has ? "이미 있음" : "끝에 추가")})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
    }
}
