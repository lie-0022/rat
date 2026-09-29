using RatGame.Data;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 도전과제 에셋 만들기 (고양이 219, plan cat-218) — docs/11 10종 중 새 루프에 맞춘 8종. 있으면 값만 맞춘다.
    /// 코인(ach_rich)·동료 비틀(ach_troll)은 판단 부탁 14 뒤. 보상 스킨은 표시만(잠금 없음).
    /// </summary>
    public static class AchievementTools
    {
        private const string Folder = "Assets/_Project/Resources/Achievements";

        private static readonly (string id, string title, string desc, string stat, int target, string skin)[] Table =
        {
            ("ach_first_extract", "첫 이사", "귀환하거나 벽 속 스테이지를 하나 넘기기", "extracts", 1, "skin_yellow"),
            ("ach_egg_courier", "계란 택배", "한 판에 계란 3개를 깨지 않고 가져오기", "eggsInOneRun", 3, null),
            ("ach_ghost", "유령 쥐", "고양이에게 한 번도 쫓기지 않고 귀환하거나 스테이지 넘기기", "ghostClears", 1, null),
            ("ach_medic", "구급 쥐", "쓰러진 동료를 10번 살리기", "revives", 10, null),
            ("ach_deep_rat", "깊은 쥐", "벽 속 스테이지 4에 닿기", "deepestStage", 4, "skin_black"),
            ("ach_chef_enemy", "요리사의 적", "로스트치킨을 가져오기", "chickenDeposits", 1, null),
            ("ach_survivor", "혼자 살아남기", "혼자 연 방에서 귀환하거나 엔딩 보기", "soloReturns", 1, "skin_white"),
            ("ach_collector", "수집가", "도감 15종 채우기", "codexCount", 15, null),
        };

        [MenuItem("Tools/RatGame/Meta/Create Achievements")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Achievements");
            int made = 0;
            foreach (var (id, title, desc, stat, target, skin) in Table)
            {
                string path = $"{Folder}/{id}.asset";
                var a = AssetDatabase.LoadAssetAtPath<AchievementSO>(path);
                if (a == null) { a = ScriptableObject.CreateInstance<AchievementSO>(); AssetDatabase.CreateAsset(a, path); made++; }
                a.Id = id; a.Title = title; a.Desc = desc; a.StatKey = stat; a.Target = target;
                a.RewardSkin = null;
                if (skin != null)
                    foreach (var g in AssetDatabase.FindAssets("t:SkinSO"))
                    {
                        var s = AssetDatabase.LoadAssetAtPath<SkinSO>(AssetDatabase.GUIDToAssetPath(g));
                        if (s.Id == skin) a.RewardSkin = s;
                    }
                EditorUtility.SetDirty(a);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rat] 도전과제 에셋: {Table.Length}종 (새 {made})");
        }
    }
}
