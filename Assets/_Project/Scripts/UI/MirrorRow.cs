using System;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>거울 한 줄 — 색 견본·이름·착용 버튼.</summary>
    public class MirrorRow : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _swatch;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private UnityEngine.UI.Button _equipButton;

        private string _id;

        public void Bind(string id, string displayName, Color color, Action<string> onEquip)
        {
            _id = id;
            _swatch.color = color;
            _nameText.text = displayName;
            _equipButton.onClick.RemoveAllListeners();
            _equipButton.onClick.AddListener(() => onEquip?.Invoke(_id));
        }

        public void Refresh(string currentId)
        {
            bool equipped = currentId == _id;
            string text = equipped ? "착용 중" : "착용";
            if (_buttonText.text != text) _buttonText.text = text;
            _equipButton.interactable = !equipped;
        }
    }
}
