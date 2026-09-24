using RatGame.Data;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>Tools/RatGame/Create Downed Body — 쓰러진 동료 대리 몸 데이터·프리팹 + Player 프리팹 연결 (고양이 46).</summary>
    public static class DownedBodyTools
    {
        private const string SoPath = "Assets/_Project/Data/Items/downed_body.asset";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/DownedBody.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        [MenuItem("Tools/RatGame/Create Downed Body")]
        public static void Create()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            var so = AssetDatabase.LoadAssetAtPath<LootItemSO>(SoPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<LootItemSO>();
                AssetDatabase.CreateAsset(so, SoPath);
            }
            var sso = new SerializedObject(so);
            sso.FindProperty("_id").stringValue = "downed_body";
            sso.FindProperty("_displayName").stringValue = "쓰러진 동료";
            sso.FindProperty("_baseValue").intValue = 0;
            sso.FindProperty("_mass").floatValue = 3f;          // docs/04 "mass 3 — 1인 운반 가능(느림)"
            sso.FindProperty("_tier").enumValueIndex = (int)LootTier.Large; // 대형처럼 끈다 (자동 자리)
            sso.ApplyModifiedPropertiesWithoutUndo();

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DownedBody";
            go.layer = LayerMask.NameToLayer("Carryable");
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();
            go.AddComponent<NetworkRigidbody>();
            var item = go.AddComponent<CarryableItem>();
            var iso = new SerializedObject(item);
            iso.FindProperty("_balance").objectReferenceValue = balance;
            iso.FindProperty("_data").objectReferenceValue = so;
            iso.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<DownedBody>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            sso.FindProperty("_prefab").objectReferenceValue = prefab;
            sso.ApplyModifiedPropertiesWithoutUndo();

            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            var comp = root.GetComponent<PlayerDownedBody>() ?? root.AddComponent<PlayerDownedBody>();
            comp.EditorSetup(prefab);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[RatGame] 다운 대리 몸 프리팹·데이터 생성, Player 프리팹 연결");
        }
    }
}
