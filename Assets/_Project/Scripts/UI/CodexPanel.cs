using System.Collections.Generic;
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

        public bool IsOpen => _root.activeSelf;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _closeButton.onClick.AddListener(Close);
            _root.SetActive(false);
        }

        public void Open(ItemDatabase database)
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() : null;
            _codex = player != null ? player.GetComponent<PlayerCodex>() : null;
            if (_rows.Count == 0) BuildRows(database);
            Refresh();
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
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
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
            _countText.text = $"도감 {unlocked}/{_rows.Count}";
        }
    }
}
