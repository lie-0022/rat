using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 뼈대 없는 쥐 모델에 걷는 느낌 (2026-09-24 고양이 71) — 모델 자식만 흔든다: 통통 튀기·좌우 뒤뚱·앞으로 기울기, 멈추면 숨쉬기.
    /// 보이는 이동 속도(위치 변화)로 계산해서 원격 사본(보간된 위치)도 똑같이 흔들린다 — 동기화 없음, 물리·콜라이더 그대로.
    /// 어색하게 뒤뚱거리는 건 웃기면 사양 (docs/00 필러 1).
    /// </summary>
    public class RatModelWobble : MonoBehaviour
    {
        [SerializeField] private Transform _model;              // RatModel 자식
        [SerializeField] private float _stepsPerMeter = 1.6f;   // 1m에 발걸음 수 (뒤뚱 주기)
        [SerializeField] private float _bobHeight = 0.06f;      // 튀는 높이 (모델 로컬, 루트 스케일 곱해짐)
        [SerializeField] private float _waddleDegrees = 9f;     // 좌우 뒤뚱 각도
        [SerializeField] private float _leanPerSpeed = 2.2f;    // 속도(m/s)당 앞으로 기울기 각도
        [SerializeField] private float _maxLean = 14f;
        [SerializeField] private float _breathe = 0.015f;       // 멈췄을 때 숨쉬기 크기

        private Vector3 _basePos;
        private Quaternion _baseRot;
        private Vector3 _baseScale;
        private Vector3 _lastPos;
        private float _phase;
        private float _speed;

        private void Awake()
        {
            if (_model == null) { enabled = false; return; }
            _basePos = _model.localPosition; _baseRot = _model.localRotation; _baseScale = _model.localScale;
            _lastPos = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 delta = transform.position - _lastPos; delta.y = 0f;
            _lastPos = transform.position;
            // 텔레포트 같은 튐은 걸음으로 치지 않는다
            float raw = delta.magnitude > 2f ? 0f : delta.magnitude / dt;
            _speed = Mathf.Lerp(_speed, raw, 1f - Mathf.Exp(-12f * dt));
            _phase += _speed * _stepsPerMeter * Mathf.PI * dt;

            float move = Mathf.Clamp01(_speed / 2f); // 2m/s 이상이면 최대 흔들림
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * _bobHeight * move;
            float waddle = Mathf.Sin(_phase) * _waddleDegrees * move;
            float lean = Mathf.Min(_speed * _leanPerSpeed, _maxLean);
            float breathe = 1f + Mathf.Sin(Time.time * 2.4f) * _breathe * (1f - move);

            _model.localPosition = _basePos + Vector3.up * bob;
            _model.localRotation = _baseRot * Quaternion.Euler(lean, 0f, waddle);
            _model.localScale = new Vector3(_baseScale.x * (2f - breathe), _baseScale.y * breathe, _baseScale.z);
        }

        /// <summary>테스트·디버그: 지금 보이는 속도.</summary>
        public float VisibleSpeed => _speed;

#if UNITY_EDITOR
        public void EditorSetup(Transform model) => _model = model;
#endif
    }
}
