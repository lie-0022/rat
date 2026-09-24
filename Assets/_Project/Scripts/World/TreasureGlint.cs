using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 보물방 금빛 반짝임 (고양이 88) — 목적지방 따뜻한 불빛(가만히 있음)과 헷갈리지 않게 빛이 일렁인다. 연출만, 동기화 없음.
    /// </summary>
    public class TreasureGlint : MonoBehaviour
    {
        [SerializeField] private Light _light;
        [SerializeField] private float _base = 6f;
        [SerializeField] private float _swing = 2f;
        [SerializeField] private float _speed = 2.3f;

        private void Start() => RatGame.Core.Log.Dev($"보물방 불빛 연출: {transform.position:F0}"); // 2인 검증용

        private void Update()
        {
            if (_light == null) return;
            // 두 사인을 섞어 규칙적인 깜빡임이 아니라 반짝임처럼
            float t = Time.time * _speed;
            _light.intensity = _base + _swing * (0.6f * Mathf.Sin(t) + 0.4f * Mathf.Sin(t * 2.7f + 1.3f));
        }

#if UNITY_EDITOR
        public void EditorSetup(Light light) => _light = light;
#endif
    }
}
