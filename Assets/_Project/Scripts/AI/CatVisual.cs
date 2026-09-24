using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 그레이박스 텔레그래프 (docs/07 연출 계약, design/cat-design/01-5). CatAnimatorLink가 생기기 전 대체 —
    /// 상태·잠 단계 NetworkVariable을 읽어 몸 색·꼬리 움직임·몸 펄스로 표현한다. 전 클라에서 돈다(읽기만).
    ///  Sleep 얕음: 꼬리 천천히 / ToDeep: 몸이 가라앉음 / Deep: 배 오르내림(펄스) / ToLight: 꼬리 씰룩 급회전(= "나가!" 신호) / HalfAwake: 머리 들림
    ///  Curious 연두(꼬리 파르르, 앞발 칠 때 몸이 앞으로 튐) / Suspicious 노랑 / Search 연노랑(킁킁 — 코 들썩임 대신 꼬리 낮게 흔듦) / Chase 빨강 / Capture 진빨강 / Distracted 하늘 / 그 외 기본 주황.
    /// </summary>
    public class CatVisual : MonoBehaviour
    {
        [SerializeField] private CatBrain _brain;
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _tail;
        [SerializeField] private Renderer _bodyRenderer;

        private static readonly Color Base = new(0.95f, 0.55f, 0.2f);
        private static readonly Color Suspicious = new(1f, 0.85f, 0.2f);
        private static readonly Color Search = new(1f, 0.95f, 0.55f);
        private static readonly Color Curious = new(0.6f, 0.9f, 0.4f);
        private static readonly Color Track = new(1f, 0.7f, 0.45f);
        private static readonly Color Stunned = new(0.5f, 0.6f, 0.8f);
        private static readonly Color Chase = new(0.9f, 0.15f, 0.1f);
        private static readonly Color Capture = new(0.6f, 0.05f, 0.05f);
        private static readonly Color Distracted = new(0.4f, 0.75f, 1f);
        private static readonly Color Asleep = new(0.75f, 0.5f, 0.3f);

        private MaterialPropertyBlock _block;
        private Vector3 _visualBaseScale;
        private Vector3 _visualBasePos;
        private Quaternion _visualBaseRot = Quaternion.identity;
        private byte _lastPawTick;
        private CatBlunderKind _lastBlunder;
        private bool _lastAlert;
        private bool _lastFighting;
        private Renderer[] _renderers;
        private int _lastRenderMode = 2;
        private float _pawKickUntil; // 앞발 칠 때 몸이 앞으로 튀는 순간

        private void Awake()
        {
            if (_brain == null) _brain = GetComponent<CatBrain>();
            _block = new MaterialPropertyBlock();
            if (_visual != null) { _visualBaseScale = _visual.localScale; _visualBasePos = _visual.localPosition; _visualBaseRot = _visual.localRotation; }
        }

        private void Update()
        {
            if (_brain == null || !_brain.IsSpawned) return;
            // 집주인이 불러 문 밖에 있으면 안 보인다
            bool show = !_brain.AwayHidden.Value;
            bool tailOnly = _brain.AmbushHidden.Value; // 매복 — 꼬리만 삐져나온다 (텔레그래프)
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            int mode = !show ? 0 : tailOnly ? 1 : 2;
            if (mode != _lastRenderMode)
            {
                _lastRenderMode = mode;
                foreach (var r in _renderers) r.enabled = mode == 2 || (mode == 1 && _tail != null && r.transform.IsChildOf(_tail));
                RatGame.Core.Log.Dev($"고양이 모습: {(mode == 0 ? "숨김" : mode == 1 ? "꼬리만" : "보임")}"); // 2인 검증용
            }
            var state = _brain.State.Value;
            var phase = _brain.SleepPhase.Value;

            Color baseColor = _brain.Personality != null ? _brain.Personality.BodyColor : Base;
            Color c = state switch
            {
                CatState.Sleep => Color.Lerp(baseColor, Asleep, 0.5f),
                CatState.Suspicious => Suspicious,
                CatState.Search => Search,
                CatState.Curious => Curious,
                CatState.Track => Track,
                CatState.Chase => Chase,
                CatState.Capture => Capture,
                CatState.Toy => Capture,
                CatState.Fight => Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? Chase : Suspicious, // 하악! 번쩍
                CatState.Blunder => _brain.BlunderKind.Value == CatBlunderKind.Stun ? Stunned : baseColor,
                CatState.Distracted => Distracted,
                _ => baseColor
            };
            if (_bodyRenderer != null)
            {
                _block.SetColor("_BaseColor", c);
                _bodyRenderer.SetPropertyBlock(_block);
            }

            float t = Time.time;
            float tailSwing = 0f, pulse = 0f, sink = 0f, roll = 0f;
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
                    else if (state == CatState.Zoomies) { tailSwing = Mathf.Sin(t * 16f) * 35f; roll = Mathf.Sin(t * 12f) * 8f; } // 우다다
                    else if (state == CatState.Fight) { roll = Mathf.Sin(t * 25f) * 12f; tailSwing = Mathf.Sin(t * 20f) * 50f; } // 몸싸움 — 부르르
                    else if (state == CatState.Patrol && _brain.Alert.Value) tailSwing = Mathf.Sin(t * 7f) * 22f; // 예민 — 꼬리가 빠르다
                    else if (state == CatState.Blunder)
                    {
                        switch (_brain.BlunderKind.Value)
                        {
                            case CatBlunderKind.Slip: roll = 35f; break;                                  // 옆으로 기울어 쭉
                            case CatBlunderKind.Stun: roll = Mathf.Sin(t * 9f) * 15f; tailSwing = Mathf.Sin(t * 4f) * 60f; break; // 빙글
                            case CatBlunderKind.Startle: sink = -0.25f; break;                           // 펄쩍
                            case CatBlunderKind.Wobble: roll = Mathf.Sin(t * 2.5f) * 20f; break;          // 휘청
                            case CatBlunderKind.Hairball: sink = 0.1f * Mathf.Abs(Mathf.Sin(t * 9f)); break; // 웩웩 — 몸 들썩
                        }
                    }
                    else if (state == CatState.Toy) tailSwing = Mathf.Sin(t * 2f) * 45f; // 놀이 — 꼬리 느리고 크게
                    else if (_brain.Sniffing.Value || state == CatState.Track) { tailSwing = Mathf.Sin(t * 3f) * 20f; sink = 0.06f + 0.03f * Mathf.Sin(t * 8f); } // 기억 칸 킁킁
                    else if (state == CatState.Curious) tailSwing = Mathf.Sin(t * 18f) * 10f; // 꼬리 곧추 파르르
                    else if (state == CatState.Search) { tailSwing = Mathf.Sin(t * 3f) * 20f; sink = 0.06f + 0.03f * Mathf.Sin(t * 8f); } // 코를 바닥에 대고 킁킁
                    break;
            }
            if (_tail != null) _tail.localRotation = Quaternion.Euler(0f, tailSwing, 20f);
            bool fighting = _brain.State.Value == CatState.Fight;
            if (fighting != _lastFighting) { _lastFighting = fighting; if (fighting) RatGame.Core.Log.Dev($"고양이 싸움 연출: {name}"); } // 2인 검증용
            if (_brain.Alert.Value != _lastAlert)
            {
                _lastAlert = _brain.Alert.Value;
                RatGame.Core.Log.Dev($"고양이 예민: {_lastAlert}"); // 2인 검증용
            }
            if (_brain.BlunderKind.Value != _lastBlunder)
            {
                _lastBlunder = _brain.BlunderKind.Value;
                if (_lastBlunder != CatBlunderKind.None) RatGame.Core.Log.Dev($"고양이 실패 연출: {_lastBlunder}"); // 2인 검증용
            }
            if (_brain.PawTick.Value != _lastPawTick)
            {
                _lastPawTick = _brain.PawTick.Value;
                _pawKickUntil = Time.time + 0.15f;
                RatGame.Core.Log.Dev($"고양이 앞발 연출 #{_lastPawTick}"); // 2인 검증용 — 클라에서도 찍힌다
            }
            float kick = Time.time < _pawKickUntil ? 0.18f : 0f;
            if (_visual != null)
            {
                _visual.localScale = _visualBaseScale + new Vector3(pulse, 0f, pulse);
                _visual.localPosition = _visualBasePos + Vector3.down * sink + Vector3.forward * kick;
                _visual.localRotation = _visualBaseRot * Quaternion.Euler(0f, 0f, roll);
            }
        }
    }
}
