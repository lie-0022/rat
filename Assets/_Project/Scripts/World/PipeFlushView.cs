using RatGame.Core;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 배관 물 보이기 (고양이 98) — 집주인 사건 "배관 물"(97) 동안 관 바닥에 물이 흐른다. 알림은 이미 전원에게 오는
    /// EventBus.HouseEvent라 동기화 추가 없음. 물은 콜라이더 없는 표시일 뿐(젖음 판정은 호스트 HouseEventDirector).
    /// </summary>
    public class PipeFlushView : MonoBehaviour
    {
        [SerializeField] private GameObject _water;

        private float _baseY;

        private void Awake()
        {
            if (_water == null) return;
            _baseY = _water.transform.localScale.y;
            _water.SetActive(false);
        }

        private void OnEnable() => EventBus.HouseEvent += OnHouseEvent;
        private void OnDisable() => EventBus.HouseEvent -= OnHouseEvent;

        private void OnHouseEvent(HouseEventKind kind, HouseEventPhase phase)
        {
            if (kind != HouseEventKind.Flush || _water == null) return;
            if (phase == HouseEventPhase.Start) { _water.SetActive(true); Log.Dev($"배관 물 연출: {transform.position:F0}"); } // 2인 검증용
            else if (phase == HouseEventPhase.End) _water.SetActive(false);
        }

        private void Update()
        {
            if (_water == null || !_water.activeSelf) return;
            // 출렁임 — 물이 흐르는 느낌만 (높이 ±30%)
            var s = _water.transform.localScale;
            s.y = _baseY * (1f + 0.3f * Mathf.Sin(Time.time * 7f));
            _water.transform.localScale = s;
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject water) => _water = water;
#endif
    }
}
