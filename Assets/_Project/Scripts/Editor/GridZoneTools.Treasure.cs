using RatGame.Data;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 보물방 표시 (고양이 88): 따뜻한 금빛 점광 + 반짝이는 부스러기 몇 개. 옆 방에서 문틈으로 빛이 새어 "저기 뭐가 있다"가 보이게.
    /// 콜라이더 없음(물리·소음·길찾기 그대로). 네트워크 오브젝트라 호스트가 스폰하면 클라도 본다 — 목록 등록은 NGO 자동.
    /// </summary>
    public static partial class GridZoneTools
    {
        private const string TreasureGlowPath = RoomDir + "/Treasure_Glow.prefab";
        private static readonly Color GlowColor = new(1f, 0.9f, 0.25f); // 치즈 노랑 — 목적지방 복숭아빛(1, 0.8, 0.55)과 구분

        [MenuItem("Tools/RatGame/Zone/Create Treasure Glow")]
        public static void CreateTreasureGlow()
        {
            var root = new GameObject("Treasure_Glow");
            root.AddComponent<Unity.Netcode.NetworkObject>();
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.color = GlowColor; light.range = 9f; light.intensity = 6f; // 낮 햇빛 아래서도 옆 방 문틈으로 보이게
            light.shadows = LightShadows.None; // 방마다 그림자 광원이 늘면 비싸다 — 빛 번짐만
            root.AddComponent<World.TreasureGlint>().EditorSetup(light); // 일렁임 — 목적지 불빛과 구분
            var rng = new System.Random(88);
            for (int i = 0; i < 5; i++)
            {
                var crumb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(crumb.GetComponent<Collider>());
                crumb.name = $"Glint_{i}";
                crumb.transform.SetParent(root.transform, false);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.4f + (float)rng.NextDouble() * 1.2f;
                crumb.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r);
                crumb.transform.localScale = Vector3.one * (0.08f + (float)rng.NextDouble() * 0.06f);
                ZoneTools.Tint(crumb, GlowColor);
                crumb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TreasureGlowPath);
            Object.DestroyImmediate(root);

            var zone = AssetDatabase.LoadAssetAtPath<GridZoneSO>(ZonePath);
            zone.TreasureGlow = prefab;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RatGame] 보물방 표시 → {TreasureGlowPath}, Zone_Walls.TreasureGlow 연결");
        }
    }
}
