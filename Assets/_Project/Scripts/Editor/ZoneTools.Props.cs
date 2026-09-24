using RatGame.AI;
using RatGame.Data;
using RatGame.World;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// 생성 방 소품 (고양이 60): 숨을 곳 2종(빈 상자·장화)과 어둠 구역 4×4를 네트워크 프리팹으로 만들어 존 정의에 연결.
    /// 방 프리팹엔 자리(HideSpawns·DarkZone)만 있고, ZoneBuilder가 시드로 골라 스폰한다. 창고 데모(CatTools)와 같은 크기·수용 인원.
    /// </summary>
    public static partial class ZoneTools
    {
        private const string PropDir = "Assets/_Project/Prefabs/Zones";

        private const string MatDir = "Assets/_Project/Art/Materials/Greybox";

        // 프리팹은 메모리 머티리얼을 저장하지 못한다(씬은 된다) — 색마다 머티리얼 에셋으로 (고양이 58에서 방이 마젠타로 나와서)
        internal static Material GreyboxMat(Color c, Shader shader)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Greybox");
            string name = "Greybox_" + ColorUtility.ToHtmlStringRGB(c);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(shader) { color = c, name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        internal static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = GreyboxMat(c, r.sharedMaterial.shader);
        }

        // 씬 오브젝트를 프리팹으로 뽑기 전에: 에셋이 아닌 머티리얼(씬에만 저장된 것)을 같은 색 에셋으로 바꾼다
        internal static void PersistMaterials(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || AssetDatabase.Contains(m)) continue;
                r.sharedMaterial = GreyboxMat(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color, m.shader);
            }
        }

        [MenuItem("Tools/RatGame/Zone/Create Room Props (Hide·Dark)")]
        public static void CreateRoomProps()
        {
            if (!AssetDatabase.IsValidFolder(PropDir)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Zones");
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinitionSO>(ZonePath);

            var box = HidePrefab(balance, "Hide_Box", "빈 상자", 2, new Vector3(1.8f, 1.5f, 1.8f), new Color(0.45f, 0.3f, 0.2f), boxSpot: true);
            var shoe = HidePrefab(balance, "Hide_Shoe", "장화", 1, new Vector3(1.1f, 1.4f, 2.2f), new Color(0.2f, 0.25f, 0.3f), boxSpot: false);
            var dark = DarkPrefab(4f);

            zone.HideSpotPrefabs = new[] { box, shoe };
            zone.DarkZonePrefab = dark;
            zone.DarkZoneChance = 0.6f;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Debug.Log("[RatGame] 방 소품 — Hide_Box·Hide_Shoe·DarkZone_4 → Zone_Kitchen_Greybox");
        }

        // HideSpot은 루트 스케일을 크기로 쓴다(나오는 자리·바닥 높이 계산) — 창고 데모와 같은 구조
        private static GameObject HidePrefab(BalanceConfigSO balance, string name, string label, int capacity, Vector3 size, Color color, bool boxSpot)
        {
            var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
            h.name = name;
            h.transform.position = new Vector3(0f, size.y * 0.5f, 0f);
            h.transform.localScale = size;
            h.GetComponent<BoxCollider>().isTrigger = true;
            Tint(h, color);
            h.AddComponent<Unity.Netcode.NetworkObject>();
            h.AddComponent<HideSpot>().EditorSetup(balance, label, capacity);
            if (boxSpot)
            {
                // 고양이 "상자 입구에 앉기"(Box 스팟) — 입구 1.4m 앞 바닥. 스케일된 부모 기준이라 나눠서 넣는다
                var spot = new GameObject("Spot_Box").transform;
                spot.SetParent(h.transform, false);
                spot.localPosition = new Vector3(0f, -0.5f, 1.4f / size.z);
                spot.gameObject.AddComponent<CatSpot>().EditorSetup(CatSpotType.Box, 0.5f);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(h, $"{PropDir}/{name}.prefab");
            Object.DestroyImmediate(h);
            return prefab;
        }

        private static GameObject DarkPrefab(float size)
        {
            var dz = new GameObject($"DarkZone_{size:0}");
            dz.layer = LayerMask.NameToLayer("Ignore Raycast");
            var col = dz.AddComponent<BoxCollider>();
            col.isTrigger = true; col.center = new Vector3(0f, 1.5f, 0f); col.size = new Vector3(size, 3f, size);
            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "DarkFloor"; mark.transform.SetParent(dz.transform, false);
            mark.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            mark.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mark.transform.localScale = new Vector3(size, size, 1f);
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            Tint(mark, new Color(0.04f, 0.04f, 0.1f));
            var mr = mark.GetComponent<Renderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dz.AddComponent<Unity.Netcode.NetworkObject>();
            dz.AddComponent<LightZone>().EditorSetup(mr);
            var prefab = PrefabUtility.SaveAsPrefabAsset(dz, $"{PropDir}/{dz.name}.prefab");
            Object.DestroyImmediate(dz);
            return prefab;
        }
    }
}
