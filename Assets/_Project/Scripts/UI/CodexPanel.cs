using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 도감 패널 (uGUI, 로컬). ItemDatabase의 전리품(Id "loot_"로 시작 — 그레이박스·테스트 제외)을 스크롤 목록으로.
    /// 해금 여부는 내 PlayerCodex(개인 저장). 미해금은 "???".
    /// </summary>
    public class CodexPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private CodexRow _rowPrefab;
        [SerializeField] private UnityEngine.UI.Button _closeButton;

        private readonly List<(LootItemSO item, CodexRow row)> _rows = new();
        private PlayerCodex _codex;
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

        public void Open(ItemDatabase database)
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() : null;
            _codex = player != null ? player.GetComponent<PlayerCodex>() : null;
            if (_rows.Count == 0) BuildRows(database);
            Refresh();
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
            if (_open && UiCommon.ClosePressed(_openedAt, out bool byEscape)) Close(byEscape);
        }

        private void BuildRows(ItemDatabase database)
        {
            foreach (var item in database.Items)
            {
                if (item == null || !item.Id.StartsWith("loot_")) continue;
                var row = Instantiate(_rowPrefab, _rowsParent);
                _rows.Add((item, row));
            }
        }

        private void Refresh()
        {
            int unlocked = 0;
            foreach (var (item, row) in _rows)
            {
                bool on = _codex != null && _codex.IsUnlocked(item.Id);
                if (on) unlocked++;
                row.Set(item, on);
            }
            _countText.text = Loc.F("도감 {0}/{1}", unlocked, _rows.Count);
        }
    }
}
