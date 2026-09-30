using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 새 루프 목적지 상점 패널 (uGUI, 로컬 — 고양이 65). StageShopCounter NV(지갑·택배·닫힘)만 읽고, 사기는 RequestPurchase로 요청.
    /// 자판기 ShopPanel과 같은 입력 규칙(InputFocus, E/Esc 닫기).
    /// </summary>
    public class StageShopPanel : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _walletText;
        [SerializeField] private TMP_Text _pendingText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private StageShopRow _rowPrefab;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private float _messageSeconds = 2.5f;

        private StageShopCounter _counter;
        private readonly List<StageShopRow> _rows = new();
        private float _messageUntil;
        private bool _open;
        private float _openedAt;

        public bool IsOpen => _open;

        private void Awake()
        {
            UiCommon.EnsureEventSystem();
            _closeButton.onClick.AddListener(() => Close(false));
            Loc.Changed += OnLanguageChanged;
            _root.SetActive(false);
        }

        private void OnDestroy() { Loc.Changed -= OnLanguageChanged; if (_open) InputFocus.PanelClosed(false); }

        // 줄은 처음 열 때 한 번 만든다 — 언어를 바꾸면 옛 언어로 남아서 다시 만든다 (고양이 316, 기지 상점과 같은 버그)
        private void OnLanguageChanged() { if (_counter != null) BuildRows(); }

        public void Open(StageShopCounter counter)
        {
            if (_counter != counter) { _counter = counter; BuildRows(); }
            _messageText.text = "";
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

        public void ShowMessage(bool ok, string message)
        {
            _messageText.text = message;
            if (_theme != null) _messageText.color = _theme.GetColor(ok ? UiColorRole.PositiveText : UiColorRole.DangerText);
            _messageUntil = Time.time + _messageSeconds;
        }

        private void Update()
        {
            if (!_open || _counter == null) return;
            if (UiCommon.ClosePressed(_openedAt, out bool byEscape)) { Close(byEscape); return; }
            int wallet = _counter.Wallet.Value;
            bool closed = _counter.Closed.Value;
            SetText(_walletText, closed ? Loc.T("마지막 스테이지 — 다음 맵이 없어서 팔지 않아요") : Loc.F("쓸 수 있는 식량 {0}  (남은 식량 + 할당량 넘은 만큼)", wallet));
            string pending = Loc.Unpack(_counter.PendingText.Value.ToString()); // 호스트가 묶어 보낸 목록 (고양이 202)
            SetText(_pendingText, pending.Length == 0 ? Loc.T("다음 맵에서 받을 것: 없음") : Loc.F("다음 맵 출발방에서 받을 것: {0}", pending));
            var entries = _counter.Shop.Entries;
            for (int i = 0; i < _rows.Count && i < entries.Length; i++) _rows[i].Refresh(!closed && wallet >= entries[i].Price);
            if (_messageText.text.Length > 0 && Time.time > _messageUntil) _messageText.text = "";
        }

        private void BuildRows()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
            if (_counter.Shop == null) return;
            var entries = _counter.Shop.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                int index = i;
                var row = Instantiate(_rowPrefab, _rowsParent);
                row.gameObject.SetActive(true);
                row.Bind(Loc.T(entries[i].DisplayName), Loc.T(entries[i].Description), entries[i].Price, () => _counter.RequestPurchase(index));
                _rows.Add(row);
            }
        }

        private static void SetText(TMP_Text label, string text) { if (label.text != text) label.text = text; }

#if UNITY_EDITOR
        public void EditorSetup(UiThemeSO theme, GameObject root, TMP_Text wallet, TMP_Text pending, TMP_Text message, Transform rowsParent, StageShopRow rowPrefab, UnityEngine.UI.Button close)
        { _theme = theme; _root = root; _walletText = wallet; _pendingText = pending; _messageText = message; _rowsParent = rowsParent; _rowPrefab = rowPrefab; _closeButton = close; }
#endif
    }
}
