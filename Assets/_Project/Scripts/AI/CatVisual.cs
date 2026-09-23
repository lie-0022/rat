using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 그레이박스 텔레그래프 (docs/07 연출 계약, design/cat-design/01-5). CatAnimatorLink가 생기기 전 대체 —
    /// 상태·잠 단계 NetworkVariable을 읽어 몸 색·꼬리 움직임·몸 펄스로 표현한다. 전 클라에서 돈다(읽기만).
    ///  Sleep 얕음: 꼬리 천천히 / ToDeep: 몸이 가라앉음 / Deep: 배 오르내림(펄스) / ToLight: 꼬리 씰룩 급회전(= "나가!" 신호) / HalfAwake: 머리 들림
    ///  Suspicious 노랑 / Chase 빨강 / Capture 진빨강 / Distracted 하늘 / 그 외 기본 주황.
    /// </summary>
    public class CatVisual : MonoBehaviour
    {
        [SerializeField] private CatBrain _brain;
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _tail;
        [SerializeField] private Renderer _bodyRenderer;

        private static readonly Color Base = new(0.95f, 0.55f, 0.2f);
        private static readonly Color Suspicious = new(1f, 0.85f, 0.2f);
        private static readonly Color Chase = new(0.9f, 0.15f, 0.1f);
        private static readonly Color Capture = new(0.6f, 0.05f, 0.05f);
        private static readonly Color Distracted = new(0.4f, 0.75f, 1f);
        private static readonly Color Asleep = new(0.75f, 0.5f, 0.3f);

        private MaterialPropertyBlock _block;
        private Vector3 _visualBaseScale;
        private Vector3 _visualBasePos;

        private void Awake()
        {
            if (_brain == null) _brain = GetComponent<CatBrain>();
            _block = new MaterialPropertyBlock();
            if (_visual != null) { _visualBaseScale = _visual.localScale; _visualBasePos = _visual.localPosition; }
        }

        private void Update()
        {
            if (_brain == null || !_brain.IsSpawned) return;
            var state = _brain.State.Value;
            var phase = _brain.SleepPhase.Value;

            Color baseColor = _brain.Personality != null ? _brain.Personality.BodyColor : Base;
            Color c = state switch
            {
                CatState.Sleep => Color.Lerp(baseColor, Asleep, 0.5f),
                CatState.Suspicious => Suspicious,
                CatState.Chase => Chase,
                CatState.Capture => Capture,
                CatState.Distracted => Distracted,
                _ => baseColor
            };
            if (_bodyRenderer != null)
            {
                _block.SetColor("_BaseColor", c);
                _bodyRenderer.SetPropertyBlock(_block);
            }

            float t = Time.time;
            float tailSwing = 0f, pulse = 0f, sink = 0f;
            switch (phase)
            {
                case CatSleepPhase.Light: tailSwing = Mathf.Sin(t * 1.5f) * 25f; break;              // 천천히 흔들림
                case CatSleepPhase.ToDeep: sink = 0.12f; tailSwing = Mathf.Sin(t * 0.8f) * 8f; break;  // 몸이 가라앉음
                case CatSleepPhase.Deep: sink = 0.12f; pulse = 0.06f * (0.5f + 0.5f * Mathf.Sin(t * 1.2f)); break; // 코골이 배
                case CatSleepPhase.ToLight: tailSwing = Mathf.Sin(t * 14f) * 40f; break;             // 씰룩 — 예고
                case CatSleepPhase.HalfAwake: sink = -0.08f; tailSwing = 0f; break;                   // 머리 들림
                default:
                    if (state == CatState.Chase) tailSwing = Mathf.Sin(t * 10f) * 15f;
                    else if (state == CatState.Suspicious) tailSwing = Mathf.Sin(t * 5f) * 30f;
                    break;
            }
            if (_tail != null) _tail.localRotation = Quaternion.Euler(0f, tailSwing, 20f);
            if (_visual != null)
            {
                _visual.localScale = _visualBaseScale + new Vector3(pulse, 0f, pulse);
                _visual.localPosition = _visualBasePos + Vector3.down * sink;
            }
        }
    }
}
