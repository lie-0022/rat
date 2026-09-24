using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// "벽 속" 쥐 전용 배관 (docs/10, 고양이 79): 고리를 닫는 연결 = 좁고 낮은 관 + 방 쪽 입구 판.
    /// 쥐 키 1.2m·눈 1.05m는 서서 지나가고, 고양이 NavMesh(필요 높이 2m)는 안 이어진다.
    /// </summary>
    public static partial class GridZoneTools
    {
        private const float PipeW = 1.2f, PipeH = 1.4f;

        // 문틈(Door×WallH)을 배관 구멍(PipeW×PipeH, 아래 가운데)만 남기고 막는 판 — 양옆 2조각 + 위 1조각
        private static GameObject PipeMouth(Transform room, int side, bool alongX, float fixedCoord, int layer)
        {
            var mouth = new GameObject($"PipeMouth_{"NESW"[side]}");
            mouth.transform.SetParent(room, false);
            mouth.transform.localPosition = alongX ? new Vector3(0f, 0f, fixedCoord) : new Vector3(fixedCoord, 0f, 0f);
            float sideW = (Door - PipeW) * 0.5f, off = PipeW * 0.5f + sideW * 0.5f;
            Vector3 Across(float a, float y) => alongX ? new Vector3(a, y, 0f) : new Vector3(0f, y, a);
            Vector3 Size(float w, float h) => alongX ? new Vector3(w, h, WallT) : new Vector3(WallT, h, w);
            Box(mouth.transform, "Left", Across(-off, WallH * 0.5f), Size(sideW, WallH), layer, PipeColor);
            Box(mouth.transform, "Right", Across(off, WallH * 0.5f), Size(sideW, WallH), layer, PipeColor);
            Box(mouth.transform, "Top", Across(0f, (WallH + PipeH) * 0.5f), Size(PipeW, WallH - PipeH), layer, PipeColor);
            mouth.SetActive(false);
            return mouth;
        }

        // 쥐 전용 배관: 좁은 바닥 + 낮은 벽 + 천장 판 (길이는 루트 Z 스케일)
        private static GameObject BuildPipe(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<Unity.Netcode.NetworkObject>();
            int wallLayer = LayerMask.NameToLayer("NoiseBlocker"), floorLayer = LayerMask.NameToLayer("RoomStatic");
            Box(root.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(PipeW + 2f * WallT, 0.1f, 1f), floorLayer, FloorColor);
            Box(root.transform, "Wall_L", new Vector3(-(PipeW + WallT) * 0.5f, PipeH * 0.5f, 0f), new Vector3(WallT, PipeH, 1f), wallLayer, PipeColor);
            Box(root.transform, "Wall_R", new Vector3((PipeW + WallT) * 0.5f, PipeH * 0.5f, 0f), new Vector3(WallT, PipeH, 1f), wallLayer, PipeColor);
            Box(root.transform, "Ceiling", new Vector3(0f, PipeH + WallT * 0.5f, 0f), new Vector3(PipeW + 2f * WallT, WallT, 1f), wallLayer, PipeColor);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{RoomDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
