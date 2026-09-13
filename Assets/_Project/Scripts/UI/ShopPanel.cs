using System.Collections.Generic;
using RatGame.Core;
using RatGame.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace RatGame.UI
{
    /// <summary>
    /// 자판기 상점 패널 (uGUI, 로컬). VendingMachine NetworkVariable(누계·레벨)만 읽어 그린다 — 구매는 RequestPurchase로 요청.
    /// 열면 커서를 풀고(시선·클릭 정지는 PlayerCameraRig·CarryController가 lockState로 판단), 닫으면 다시 잠근다.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _totalText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private ShopRow _rowPrefab;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private float _messageSeconds = 2f;
        [SerializeField] private Color _okColor = new(0.75f, 1f, 0.75f);
        [SerializeField] private Color _failColor = new(1f, 0.6f, 0.6f);

        private VendingMachine _machine;
        private readonly List<ShopRow> _rows = new();
        private float _messageUntil;

        public bool IsOpen => _root.activeSelf;

        private void Awake()
        {
            EnsureEventSystem();
            _closeButton.onClick.AddListener(Close);
            _root.SetActive(false);
        }

        public void Open(VendingMachine machine)
        {
            if (_machine != machine)
            {
                _machine = machine;
                BuildRows();
            }
            _root.SetActive(true);
            _messageText.text = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            _root.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ShowMessage(bool ok, string message)
        {
            _messageText.text = message;
            _messageText.color = ok ? _okColor : _failColor;
            _messageUntil = Time.time + _messageSeconds;
        }

        private void Update()
        {
            if (!IsOpen || _machine == null) return;
            // Esc: PlayerCameraRig도 같은 프레임에 커서를 토글하지만 결과는 같다 (풀림 → 잠금)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }

            int total = _machine.HaulTotal.Value;
            SetText(_totalText, $"누계 {total}");
            var levels = _machine.Levels.Value;
            for (int i = 0; i < _rows.Count; i++)
            {
                var so = _machine.Upgrades[i];
                int level = levels.Get(so.Effect);
                int price = so.PriceForLevel(level + 1);
                _rows[i].Refresh(level, so.MaxLevel, price, total >= price);
            }
            if (_messageText.text.Length > 0 && Time.time >= _messageUntil) _messageText.text = "";
        }

        private void BuildRows()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
            for (int i = 0; i < _machine.Upgrades.Length; i++)
            {
                var row = Instantiate(_rowPrefab, _rowsParent);
                row.Bind(i, _machine.Upgrades[i], _machine.RequestPurchase);
                _rows.Add(row);
            }
        }

        // 씬에 EventSystem이 없으면 버튼이 조용히 안 눌린다 (ui-ugui 스킬 규칙) — 정확히 하나만 보장
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
            Log.Dev("EventSystem 생성 (ShopPanel)");
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
