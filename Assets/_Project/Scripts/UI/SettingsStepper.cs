using System;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>설정 행의 &lt; 값 &gt; 선택기. 선택지 목록은 패널이 넣고, 바뀐 인덱스를 알린다.</summary>
    public class SettingsStepper : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _prevButton;
        [SerializeField] private UnityEngine.UI.Button _nextButton;
        [SerializeField] private TMP_Text _valueText;

        private string[] _options = Array.Empty<string>();
        private int _index;

        public event Action<int> Changed;

        private void Awake()
        {
            _prevButton.onClick.AddListener(() => Step(-1));
            _nextButton.onClick.AddListener(() => Step(1));
        }

        public void SetOptions(string[] options, int index)
        {
            _options = options;
            _index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));
            Refresh();
        }

        // 끝에서 멈춘다 (한 바퀴 돌면 해상도 목록에서 헷갈림)
        private void Step(int delta)
        {
            int next = Mathf.Clamp(_index + delta, 0, _options.Length - 1);
            if (next == _index) return;
            _index = next;
            Refresh();
            Changed?.Invoke(_index);
        }

        private void Refresh()
        {
            _valueText.text = _options.Length > 0 ? _options[_index] : "-";
            _prevButton.interactable = _index > 0;
            _nextButton.interactable = _index < _options.Length - 1;
        }
    }
}
