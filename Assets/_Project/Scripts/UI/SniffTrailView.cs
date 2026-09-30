using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 킁킁 냄새 줄기 (docs/04 Sniff, 고양이 76). EventBus.SniffHint를 받아 발밑에서 다음 문까지 작은 알갱이를 깔고,
    /// 알갱이가 문 쪽으로 흘러가며 흐려진다 — 방향이 한눈에 보이게. 내 화면에만(로컬 이벤트).
    /// </summary>
    public class SniffTrailView : MonoBehaviour
    {
        private const float Spacing = 0.6f;   // 알갱이 간격 (m)
        private const float Height = 0.25f;   // 바닥 위
        private const float Drift = 0.5f;     // 사는 동안 문 쪽으로 흘러가는 거리
        private const float Size = 0.12f;
        private const int MaxPuffs = 60;

        [SerializeField] private Material _puffMaterial;

        private sealed class Puff { public Transform T; public MeshRenderer R; public Vector3 Start; public Vector3 Dir; public float Born; public float Life; public float Scale = 1f; }

        // 값 구간별 김 색 (고양이 259) — 싼 건 옅은 회백, 중간은 재료 색(연노랑) 그대로, 비싼 건 진한 금주황. 높이·크기(160)에 색까지 겹쳐 한눈에.
        // 재료가 반투명(알파 0.75)이라 알파도 같이 준다
        private static readonly Color[] WispTint = { new(0.86f, 0.88f, 0.9f, 0.6f), Color.clear, new(1f, 0.6f, 0.1f, 0.9f) };
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        private readonly List<Puff> _pool = new();
        private readonly List<Puff> _live = new();
        private Transform _root; // 캔버스 밑에 두면 캔버스 스케일이 알갱이에 곱해져서 씬 루트에 따로

        private void Awake() => _root = new GameObject("SniffPuffs").transform;
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }

        private void OnEnable() { EventBus.SniffHint += OnSniff; EventBus.SniffFood += OnSniffFood; }
        private void OnDisable() { EventBus.SniffHint -= OnSniff; EventBus.SniffFood -= OnSniffFood; }

        private static readonly int[] WispPuffs = { 3, 4, 6 };            // 값 구간별 김 알갱이 — 비쌀수록 높이 (고양이 160)
        private static readonly float[] WispScale = { 1f, 1.15f, 1.35f };
        private const float WispRise = 0.35f;  // 알갱이 사이 높이

        // 음식 냄새 김 (고양이 150) — 줄기와 같은 알갱이가 음식 위로 차례로 떠오른다
        private void OnSniffFood(System.Collections.Generic.IReadOnlyList<Vector3> spots, System.Collections.Generic.IReadOnlyList<int> tiers, float seconds)
        {
            if (_root == null) { _root = new GameObject("SniffPuffs").transform; _pool.Clear(); _live.Clear(); }
            for (int s = 0; s < spots.Count; s++)
            {
                int tier = tiers != null && s < tiers.Count ? Mathf.Clamp(tiers[s], 0, 2) : 1;
                for (int i = 0; i < WispPuffs[tier] && _live.Count < MaxPuffs * 2; i++)
                {
                    var p = Take();
                    p.Scale = WispScale[tier];
                    Tint(p, WispTint[tier]);
                    p.Start = spots[s] + Vector3.up * (Height + i * WispRise);
                    p.Dir = Vector3.up;
                    p.Born = Time.time + 0.3f + s * 0.05f + i * 0.12f; // 줄기가 먼저 뻗고 나서
                    p.Life = seconds;
                    p.T.position = p.Start;
                    p.T.localScale = Vector3.zero;
                    p.T.gameObject.SetActive(true);
                    _live.Add(p);
                }
            }
        }

        private void OnSniff(Vector3 from, Vector3 to, float seconds)
        {
            Vector3 flat = to - from; flat.y = 0f;
            float length = flat.magnitude;
            if (length < 0.05f) return;
            if (_root == null) { _root = new GameObject("SniffPuffs").transform; _pool.Clear(); _live.Clear(); } // 씬이 바뀌어 알갱이가 같이 사라졌으면 새로
            Vector3 dir = flat / length;
            int count = Mathf.Min(MaxPuffs, Mathf.CeilToInt(length / Spacing) + 1);
            for (int i = 0; i < count; i++)
            {
                var p = Take();
                p.Start = new Vector3(from.x, 0f, from.z) + dir * Mathf.Min(length, i * Spacing) + Vector3.up * Height;
                p.Dir = dir;
                p.Born = Time.time + i * 0.04f; // 발밑부터 차례로 — 줄기가 뻗어 나가는 느낌
                p.Scale = 1f; // 풀에서 꺼낸 알갱이 크기 되돌림
                Tint(p, Color.clear); // 색도 되돌림
                p.Life = seconds;
                p.T.position = p.Start;
                p.T.localScale = Vector3.zero;
                p.T.gameObject.SetActive(true);
                _live.Add(p);
            }
        }

        private Puff Take()
        {
            if (_pool.Count > 0) { var last = _pool[_pool.Count - 1]; _pool.RemoveAt(_pool.Count - 1); return last; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>()); // 표시일 뿐 — 물리·소음 판정에 걸리면 안 된다
            go.name = "SniffPuff";
            go.transform.SetParent(_root, false);
            var r = go.GetComponent<MeshRenderer>();
            if (_puffMaterial != null) r.sharedMaterial = _puffMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Puff { T = go.transform, R = r };
        }

        // clear = 재료 색 그대로(블록 비움)
        private void Tint(Puff p, Color c)
        {
            if (c.a <= 0f) { p.R.SetPropertyBlock(null); return; }
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, c);
            p.R.SetPropertyBlock(_block);
        }

        private void Update()
        {
            if (_root == null) return;
            float now = Time.time;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                float t = (now - p.Born) / p.Life;
                if (t < 0f) continue;
                if (t >= 1f) { p.T.gameObject.SetActive(false); _live.RemoveAt(i); _pool.Add(p); continue; }
                float bob = Mathf.Sin((now + p.Start.x) * 6f) * 0.03f;
                p.T.position = p.Start + p.Dir * (Drift * t) + Vector3.up * bob;
                p.T.localScale = Vector3.one * (Size * p.Scale * Mathf.Sin(t * Mathf.PI)); // 나타났다 사라짐
            }
        }
    }
}
