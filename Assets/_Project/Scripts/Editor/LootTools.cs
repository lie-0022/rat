using System.Collections.Generic;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// Tools/RatGame/Create Loot Assets — docs/08 표 21종(헤어볼 포함)의 SO + 프리미티브 프리팹 + ItemDatabase 생성.
    /// 표와 어긋나면 여기와 docs를 함께 고칠 것 (docs/14 Validate Balance 후보).
    /// </summary>
    public static class LootTools
    {
        // docs/08 초기 아이템 목록 그대로
        private static readonly (string id, string name, LootTier tier, int value, float mass, ItemTrait traits, string flavor)[] Table =
        {
            ("loot_cracker",      "크래커",     LootTier.Small,   10,  0.4f, ItemTrait.None, "밟으면 바사삭. 증거인멸이 쉽다."),
            ("loot_candy",        "사탕",       LootTier.Small,   12,  0.3f, ItemTrait.None, "혀에 붙는다. 혀째로 운반하지 말 것."),
            ("loot_coin",         "동전",       LootTier.Small,   15,  0.5f, ItemTrait.None, "인간은 이걸 모으려고 평생을 쓴다더라."),
            ("loot_bottlecap",    "병뚜껑",     LootTier.Small,    8,  0.3f, ItemTrait.None, "쥐 사회의 방패. 유행은 돌고 돈다."),
            ("loot_grape",        "포도알",     LootTier.Small,   10,  0.4f, ItemTrait.Rolling, "굴러간다. 어디까지? 끝까지."),
            ("loot_cheese_bit",   "치즈 조각",  LootTier.Small,   14,  0.6f, ItemTrait.Edible, "비상식량. 하지만 지금이 비상 아닌가?"),
            ("loot_egg",          "계란",       LootTier.Tricky,  40,  2.0f, ItemTrait.Fragile | ItemTrait.Rolling, "인생과 같다. 굴리면 괜찮고 던지면 끝장."),
            ("loot_jelly",        "젤리",       LootTier.Tricky,  35,  1.8f, ItemTrait.Wobbly, "출렁출렁. 들고 있으면 멀미가 온다."),
            ("loot_plate",        "접시",       LootTier.Tricky,  45,  2.5f, ItemTrait.Fragile | ItemTrait.Slippery, "설거지 직후가 제일 위험하다."),
            ("loot_soap",         "비누",       LootTier.Tricky,  30,  1.5f, ItemTrait.Slippery, "청결한 부자가 되자. 잡을 수만 있다면."),
            ("loot_can",          "참치캔",     LootTier.Tricky,  38,  3.0f, ItemTrait.Rolling, "고양이도 이걸 노린다. 서두르자."),
            ("loot_lightbulb",    "전구",       LootTier.Tricky,  42,  1.2f, ItemTrait.Fragile, "아이디어가 떠올랐다: 조심히 들자."),
            ("loot_cheese_wheel", "치즈 덩어리", LootTier.Large,  120, 10f,  ItemTrait.Edible, "쥐 문명의 금괴. 먹으면 안 된다. 먹으면… 안 된다."),
            ("loot_baguette",     "바게트",     LootTier.Large,   90,  8f,   ItemTrait.None, "이틀 지나면 무기가 된다. 오늘은 재산이다."),
            ("loot_ham",          "햄 덩어리",  LootTier.Large,  110, 11f,   ItemTrait.None, "왜 무거운 것은 다 맛있는가."),
            ("loot_watermelon",   "수박 조각",  LootTier.Large,  160, 18f,   ItemTrait.Slippery, "즙이 손잡이를 배신한다."),
            ("loot_chicken",      "로스트치킨", LootTier.Large,  200, 22f,   ItemTrait.None, "4인 운반 권장. 4인 시식 금지."),
            ("loot_ring",         "반지",       LootTier.Special, 250, 0.3f, ItemTrait.None, "고양이 옆에서 반짝인다. 함정 같지만 진짜다."),
            ("loot_phone",        "스마트폰",   LootTier.Special, 220, 5f,   ItemTrait.Alarming, "만지면 운다. 인간도 그렇다."),
            ("loot_watch",        "회중시계",   LootTier.Special, 180, 1.5f, ItemTrait.Alarming, "똑딱똑딱. 고양이 귀에도 똑딱똑딱."),
            // 스폰 테이블엔 없음 — 고양이가 그루밍 뒤 뱉는다 (2026-09-24, design/cat-ideas/11)
            ("loot_hairball",     "헤어볼",     LootTier.Small,    5,  0.3f, ItemTrait.Slippery, "고양이가 준 선물. 받기 싫었다."),
        };

        [MenuItem("Tools/RatGame/Create Loot Assets")]
        public static void CreateLootAssets()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            // 이미 있으면 만들지 않는다 — CreateFolder는 같은 이름이 있으면 "Loot 1"을 새로 만든다
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/Items/Loot"))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/Items", "Loot");
            var all = new List<LootItemSO>();

            foreach (var row in Table)
            {
                // 1) SO
                string soPath = $"Assets/_Project/Data/Items/{row.id}.asset";
                var so = AssetDatabase.LoadAssetAtPath<LootItemSO>(soPath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<LootItemSO>();
                    AssetDatabase.CreateAsset(so, soPath);
                }
                var s = new SerializedObject(so);
                s.FindProperty("_id").stringValue = row.id;
                s.FindProperty("_displayName").stringValue = row.name;
                s.FindProperty("_codexFlavor").stringValue = row.flavor;
                s.FindProperty("_baseValue").intValue = row.value;
                s.FindProperty("_mass").floatValue = row.mass;
                s.FindProperty("_traits").intValue = (int)row.traits;
                s.FindProperty("_tier").enumValueIndex = (int)row.tier;
                s.ApplyModifiedPropertiesWithoutUndo();

                // 2) 프리팹 (임시 프리미티브 — 아트 단계에서 교체)
                string prefabPath = $"Assets/_Project/Prefabs/Items/Loot/{row.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    var prim = (row.traits & ItemTrait.Rolling) != 0 ? PrimitiveType.Sphere
                        : row.id == "loot_plate" ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                    var go = GameObject.CreatePrimitive(prim);
                    go.name = row.id;
                    go.layer = LayerMask.NameToLayer("Carryable");
                    go.transform.localScale = Vector3.one * Mathf.Clamp(0.3f + row.mass * 0.06f, 0.3f, 1.7f);

                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = row.mass;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    go.AddComponent<NetworkObject>();
                    go.AddComponent<NetworkTransform>();
                    go.AddComponent<NetworkRigidbody>();

                    int gripCount = row.tier == LootTier.Large ? (row.mass >= 16f ? 4 : 2) : 1;
                    var grips = new Transform[gripCount];
                    for (int i = 0; i < gripCount; i++)
                    {
                        var grip = new GameObject($"Grip_{i}").transform;
                        grip.SetParent(go.transform, false);
                        float angle = (360f / gripCount) * i * Mathf.Deg2Rad;
                        grip.localPosition = new Vector3(Mathf.Cos(angle) * 0.5f, 0.5f, Mathf.Sin(angle) * 0.5f);
                        grips[i] = grip;
                    }

                    var item = go.AddComponent<CarryableItem>();
                    var iso = new SerializedObject(item);
                    iso.FindProperty("_data").objectReferenceValue = so;
                    iso.FindProperty("_balance").objectReferenceValue = balance;
                    var gp = iso.FindProperty("_gripPoints");
                    gp.arraySize = grips.Length;
                    for (int i = 0; i < grips.Length; i++)
                        gp.GetArrayElementAtIndex(i).objectReferenceValue = grips[i];
                    iso.ApplyModifiedPropertiesWithoutUndo();

                    prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    Object.DestroyImmediate(go);
                }

                var s2 = new SerializedObject(so);
                s2.FindProperty("_prefab").objectReferenceValue = prefab;
                s2.ApplyModifiedPropertiesWithoutUndo();
                all.Add(so);
            }

            // 3) ItemDatabase
            string dbPath = "Assets/_Project/Data/Items/ItemDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            db.EditorSetItems(all);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RatGame] 전리품 {all.Count}종 + ItemDatabase 생성 완료");
        }
    }
}
