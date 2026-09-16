using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 월드 지점 → 오버레이 캔버스 위치 (핑 마커·의심 표시 공용, production/plans/ui-03·04).
    /// 결과는 캔버스 중심 기준 좌표(앵커·피벗 0.5). 화면 밖이거나 카메라 뒤면 그 방향 가장자리에 붙인다.
    /// 가장자리 여백은 변마다 따로 — 위(RunBar)·아래(인벤 슬롯·CarryInfo)에 겹치지 않게.
    /// </summary>
    public static class ScreenAnchor
    {
        /// <summary>가장자리 여백 (캔버스 단위, 1920×1080 기준).</summary>
        public struct Margins
        {
            public float Left, Right, Bottom, Top;
            public Margins(float left, float right, float bottom, float top) { Left = left; Right = right; Bottom = bottom; Top = top; }
        }

        public static Vector2 ToCanvas(Camera cam, RectTransform canvasRect, Vector3 world, Margins margins, out bool onScreen)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0f;
            onScreen = !behind && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;

            Vector2 size = canvasRect.rect.size;
            Vector2 p;
            if (behind)
            {
                // 뒤집힌 화면 좌표는 바로 뒤에서 방향이 흔들린다 — 나침반처럼 카메라 기준 좌우(x)·앞뒤(z)로. 뒤는 아래쪽
                Vector3 local = cam.transform.InverseTransformPoint(world);
                p = new Vector2(local.x, local.z);
                if (p.sqrMagnitude < 0.0001f) p = Vector2.down;
            }
            else
            {
                p = new Vector2(sp.x / Screen.width * size.x, sp.y / Screen.height * size.y) - size * 0.5f;
                if (onScreen) return p;
                if (p.sqrMagnitude < 1f) p = Vector2.down;
            }
            Vector2 half = size * 0.5f;
            float maxX = p.x >= 0f ? half.x - margins.Right : half.x - margins.Left;
            float maxY = p.y >= 0f ? half.y - margins.Top : half.y - margins.Bottom;
            float scale = Mathf.Min(maxX / Mathf.Max(Mathf.Abs(p.x), 0.001f), maxY / Mathf.Max(Mathf.Abs(p.y), 0.001f));
            // 앞쪽 화면 밖은 가장자리까지 줄이고, 뒤쪽은 항상 가장자리까지 민다
            return p * (behind ? scale : Mathf.Min(1f, scale));
        }
    }
}
