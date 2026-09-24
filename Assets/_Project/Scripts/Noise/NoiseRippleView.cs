using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.Noise
{
    /// <summary>
    /// 소음 파문 (docs/06 "loudness 40 이상이면 발생 지점에 파문 이펙트" — 2026-09-24 고양이 45). EventBus.NoiseRipple 구독자.
    /// 바닥에 전파 반경까지 퍼지는 고리 — 쥐가 "방금 저게 들렸다"를 안다. 연출만(판정 없음). 씬마다 자동 생성.
    /// </summary>
    public class NoiseRippleView : MonoBehaviour
    {
        private const float Seconds = 0.7f;
        private const int Segments = 48;

        private class Ring { public LineRenderer Line; public Vector3 Pos; public float Radius; public float Start; }
        private readonly List<Ring> _rings = new();
        private Material _mat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("NoiseRippleView");
            DontDestroyOnLoad(go);
            go.AddComponent<NoiseRippleView>();
        }

        private void OnEnable() => EventBus.NoiseRipple += OnRipple;
        private void OnDisable() => EventBus.NoiseRipple -= OnRipple;

        private void OnRipple(Vector3 pos, float loudness)
        {
            if (_mat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _mat = shader != null ? new Material(shader) : null;
            }
            Ring ring = null;
            foreach (var r in _rings) if (!r.Line.gameObject.activeSelf) { ring = r; break; }
            if (ring == null)
            {
                if (_rings.Count >= 8) ring = _rings[0]; // 너무 많으면 가장 오래된 것 재사용
                else
                {
                    var go = new GameObject("NoiseRipple");
                    go.transform.SetParent(transform);
                    var line = go.AddComponent<LineRenderer>();
                    line.loop = true; line.positionCount = Segments; line.useWorldSpace = true;
                    line.widthMultiplier = 0.06f;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (_mat != null) line.sharedMaterial = _mat;
                    ring = new Ring { Line = line };
                    _rings.Add(ring);
                }
            }
            ring.Pos = new Vector3(pos.x, 0.05f, pos.z);
            ring.Radius = Mathf.Max(0.5f, NoiseSystem.RadiusFor(loudness));
            ring.Start = Time.time;
            ring.Line.gameObject.SetActive(true);
            Log.Dev($"소음 파문 연출: {loudness:0} @ {pos:F1}"); // 2인 검증용
        }

        private void Update()
        {
            foreach (var r in _rings)
            {
                if (!r.Line.gameObject.activeSelf) continue;
                float t = (Time.time - r.Start) / Seconds;
                if (t >= 1f) { r.Line.gameObject.SetActive(false); continue; }
                float rad = Mathf.Lerp(0.2f, r.Radius, 1f - (1f - t) * (1f - t)); // 빠르게 퍼지다 느려짐
                var c = new Color(1f, 0.85f, 0.3f, 1f - t);
                r.Line.startColor = c; r.Line.endColor = c;
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    r.Line.SetPosition(i, r.Pos + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad));
                }
            }
        }
    }
}
