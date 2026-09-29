using RatGame.Player;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// [E] 홀드 게이지 (docs/12 InteractPrompt의 홀드 부분) — 구출처럼 길게 눌러야 하는 상호작용 중 조준점 아래 막대.
    /// PlayerInteractor 상태만 읽는다. 즉시 상호작용(자판기·거울·도감)은 게이지 없음.
    /// </summary>
    public class HoldGaugeWidget : MonoBehaviour
    {
        [SerializeField] private GameObject _box;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private RectTransform _fill;

        private PlayerInteractor _interactor;

        public void Bind(PlayerInteractor interactor) => _interactor = interactor;

        private void Update()
        {
            string prompt = _interactor != null ? _interactor.HoldPromptText : null;
            bool holding = prompt != null;
            if (!holding && _interactor != null) prompt = _interactor.FocusPromptText; // 누르기 전 미리 보기 (고양이 187)
            bool show = prompt != null;
            if (_box.activeSelf != show) _box.SetActive(show);
            if (!show) return;

            string text = $"[E] {prompt}";
            if (_label.text != text) _label.text = text;
            float t = holding ? _interactor.HoldProgress : 0f;
            if (!Mathf.Approximately(_fill.anchorMax.x, t)) _fill.anchorMax = new Vector2(t, 1f);
        }
    }
}
