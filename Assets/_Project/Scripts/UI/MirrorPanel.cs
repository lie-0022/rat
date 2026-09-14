using System.Collections.Generic;
using RatGame.Core;
using RatGame.Player;
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
        private bool _open;
        private float _openedAt;

        public bool IsOpen => _open;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _closeButton.onClick.AddListener(() => Close(false));
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_open) InputFocus.PanelClosed(false);
        }

        public void Open()
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() : null;
            var skin = player != null ? player.GetComponent<PlayerSkin>() : null;
            if (skin == null) return;
            if (_skin != skin) { _skin = skin; BuildRows(); }
            if (_open) return;
            _open = true;
            _openedAt = Time.unscaledTime;
            _root.SetActive(true);
            InputFocus.PanelOpened();
        }

        public void Close(bool byEscape = false)
        {
            if (!_open) return;
            _open = false;
            _root.SetActive(false);
            InputFocus.PanelClosed(byEscape);
        }

        private void Update()
        {
            if (!_open || _skin == null) return;
            if (UiCommon.ClosePressed(_openedAt, out bool byEscape)) { Close(byEscape); return; }
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
