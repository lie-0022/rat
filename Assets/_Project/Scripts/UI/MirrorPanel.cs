using System.Collections.Generic;
using RatGame.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 거울 = 스킨 선택 패널 (uGUI, 로컬). 내 PlayerSkin의 목록을 행으로 만들고, 고르면 PlayerSkin.Equip.
    /// 해금은 도전과제 미구현이라 전부 열림 (docs/11).
    /// </summary>
    public class MirrorPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private MirrorRow _rowPrefab;
        [SerializeField] private UnityEngine.UI.Button _closeButton;

        private PlayerSkin _skin;
        private readonly List<MirrorRow> _rows = new();

        public bool IsOpen => _root.activeSelf;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _closeButton.onClick.AddListener(Close);
            _root.SetActive(false);
        }

        public void Open()
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() : null;
            var skin = player != null ? player.GetComponent<PlayerSkin>() : null;
            if (skin == null) return;
            if (_skin != skin) { _skin = skin; BuildRows(); }
            _root.SetActive(true);
            UiCommon.SetCursorFree(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _root.SetActive(false);
            UiCommon.SetCursorFree(false);
        }

        private void Update()
        {
            if (!IsOpen || _skin == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            string current = _skin.CurrentId;
            foreach (var row in _rows) row.Refresh(current);
        }

        private void BuildRows()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
            // 첫 줄: 기본색 (스킨 없음)
            var basic = Instantiate(_rowPrefab, _rowsParent);
            basic.Bind("", "기본 (팀 색)", _skin.GetComponent<PlayerVisual>().DefaultColor, _skin.Equip);
            _rows.Add(basic);
            foreach (var so in _skin.Skins)
            {
                if (so == null) continue;
                var row = Instantiate(_rowPrefab, _rowsParent);
                row.Bind(so.Id, so.DisplayName, so.TintColor, _skin.Equip);
                _rows.Add(row);
            }
        }
    }
}
