using System;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>설정 행의 슬라이더 + 값 글자. 퍼센트(볼륨) 또는 배율(감도) 표시.</summary>
    public class SettingsSliderRow : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Slider _slider;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private bool _percent = true;

        public event Action<float> Changed;

        private void Awake() => _slider.onValueChanged.AddListener(OnSliderChanged);

        public void SetValue(float value)
        {
            _slider.SetValueWithoutNotify(value);
            RefreshText(_slider.value);
        }

        private void OnSliderChanged(float value)
        {
            // 감도는 0.1 단위로 맞춘다 — 표시값과 저장값이 어긋나지 않게
            if (!_percent)
            {
                float snapped = Mathf.Round(value * 10f) / 10f;
                if (!Mathf.Approximately(snapped, value)) { _slider.SetValueWithoutNotify(snapped); value = snapped; }
            }
            RefreshText(value);
            Changed?.Invoke(value);
        }

        private void RefreshText(float value) =>
            _valueText.text = _percent ? $"{Mathf.RoundToInt(value * 100f)}%" : $"{value:0.0}";
    }
}
