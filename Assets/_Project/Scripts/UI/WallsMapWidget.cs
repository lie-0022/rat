using System.Collections.Generic;
using RatGame.Core;
using RatGame.Player;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 벽 속 지도 (docs/12 WallsMap, 고양이 102). Tab을 누르고 있는 동안 가운데 지도 — 내가 들어가 본 방만(안개) + 목적지는 항상,
    /// 가 본 방의 열린 면마다 짧은 선이라 안 가 본 출구가 보인다(배관 면은 파랑). 나·동료 점.
    /// 클라에도 있는 GridRoom 위치·크기·OpenSides만 읽는 로컬 기록 — 동기화 없음, 맵(씬)이 바뀌면 새로.
    /// </summary>
    public class WallsMapWidget : MonoBehaviour
    {
        private const float MapPixels = 620f;   // 지도 긴 변(캔버스 px)
        private const float StubMeters = 1.6f;  // 출구 선 길이
        private const float StubWidthMeters = 0.9f;
        private const float VisitCheckSeconds = 0.25f;
        private const float PingMapSeconds = 10f; // 핑은 지도에 조금 더 오래 (고양이 118)

        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UnityEngine.UI.Image _roomTemplate;
        [SerializeField] private UnityEngine.UI.Image _dotTemplate;
        [SerializeField] private Color _roomColor = new(0.86f, 0.83f, 0.76f, 0.95f);
        [SerializeField] private Color _destColor = new(0.95f, 0.6f, 0.25f, 0.95f);
        [SerializeField] private Color _exitColor = new(0.86f, 0.83f, 0.76f, 1f);
        [SerializeField] private Color _pipeColor = new(0.35f, 0.6f, 0.95f, 1f); // 문 선과 확 구분되게 파랑 (어두운 바탕에서 회색끼리 안 갈려서)
        [SerializeField] private Color _treasureColor = new(1f, 0.85f, 0.25f, 0.95f); // 가 본 보물방 (고양이 103)

        private GridRoom[] _rooms = new GridRoom[0];
        private GridRoom _destination;
        private GridRoom _treasure;
        private readonly HashSet<GridRoom> _visited = new();
        private readonly Dictionary<GridRoom, GameObject> _roomViews = new();
        private readonly Dictionary<ulong, UnityEngine.UI.Image> _dots = new();
        private readonly Dictionary<ulong, (Vector3 pos, float until)> _pings = new();
        private readonly Dictionary<ulong, UnityEngine.UI.Image> _pingMarks = new();
        private Vector2 _center;
        private float _scale;
        private float _nextCheck;

        private void OnEnable() => EventBus.PingReceived += OnPing;
        private void OnDisable() => EventBus.PingReceived -= OnPing;
        private void OnPing(ulong owner, Vector3 world) => _pings[owner] = (world, Time.unscaledTime + PingMapSeconds);

        private void Start()
        {
            _roomTemplate.gameObject.SetActive(false);
            _dotTemplate.gameObject.SetActive(false);
            _panel.SetActive(false);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + VisitCheckSeconds;
                if (_rooms.Length == 0 || _rooms[0] == null) Rebuild(); // 처음·씬이 바뀌어 방이 사라졌을 때
                TrackVisit();
            }
            var kb = Keyboard.current;
            bool show = kb != null && kb.tabKey.isPressed && _rooms.Length > 0 && !InputFocus.IsUiOpen;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;
            foreach (var pair in _roomViews) pair.Value.SetActive(_visited.Contains(pair.Key) || pair.Key == _destination);
            UpdateDots();
            UpdatePings();
        }

        // 동료 핑 — 팀색 마름모 (핑 마커와 같은 모양), 10초
        private void UpdatePings()
        {
            float now = Time.unscaledTime;
            foreach (var pair in _pings)
            {
                if (!_pingMarks.TryGetValue(pair.Key, out var mark) || mark == null)
                {
                    mark = Instantiate(_roomTemplate, _content);
                    mark.color = PlayerVisual.ColorFor(pair.Key);
                    mark.rectTransform.sizeDelta = Vector2.one * 16f;
                    mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    _pingMarks[pair.Key] = mark;
                }
                bool live = now < pair.Value.until;
                if (mark.gameObject.activeSelf != live) mark.gameObject.SetActive(live);
                if (live) { mark.rectTransform.anchoredPosition = (Flat(pair.Value.pos) - _center) * _scale; mark.transform.SetAsLastSibling(); }
            }
        }

        private void Rebuild()
        {
            foreach (var v in _roomViews.Values) if (v != null) Destroy(v);
            _roomViews.Clear();
            foreach (var d in _dots.Values) if (d != null) Destroy(d.gameObject);
            _dots.Clear();
            foreach (var d in _pingMarks.Values) if (d != null) Destroy(d.gameObject);
            _pingMarks.Clear();
            _pings.Clear();
            _visited.Clear();
            _rooms = FindObjectsByType<GridRoom>(FindObjectsSortMode.None);
            if (_rooms.Length == 0) return;

            var depot = FindAnyObjectByType<DepositZone>();
            var glint = FindAnyObjectByType<TreasureGlint>(); // 보물방 불빛(고양이 88) — 클라에도 스폰돼 있어 위치로 방을 찾는다
            _destination = null; _treasure = null;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var r in _rooms)
            {
                Vector2 c = Flat(r.transform.position), half = r.Size * 0.5f;
                min = Vector2.Min(min, c - half); max = Vector2.Max(max, c + half);
                if (depot != null && Inside(r, depot.transform.position)) _destination = r;
                if (glint != null && Inside(r, glint.transform.position)) _treasure = r;
            }
            _center = (min + max) * 0.5f;
            _scale = MapPixels / Mathf.Max(max.x - min.x, max.y - min.y, 1f);

            foreach (var r in _rooms)
            {
                var img = Instantiate(_roomTemplate, _content);
                var rt = img.rectTransform;
                rt.anchoredPosition = (Flat(r.transform.position) - _center) * _scale;
                rt.sizeDelta = r.Size * _scale;
                img.color = r == _destination ? _destColor : r == _treasure ? _treasureColor : _roomColor;
                byte sides = r.OpenSides.Value;
                for (int s = 0; s < 4; s++)
                {
                    if ((sides & (1 << s)) == 0) continue;
                    bool pipe = (sides & (1 << (s + 4))) != 0;
                    var stub = Instantiate(_roomTemplate, rt);
                    stub.gameObject.SetActive(true);
                    stub.color = pipe ? _pipeColor : _exitColor;
                    Vector2 dir = s switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
                    Vector2 edge = new(dir.x * r.Size.x * 0.5f, dir.y * r.Size.y * 0.5f);
                    stub.rectTransform.anchoredPosition = (edge + dir * StubMeters * 0.5f) * _scale;
                    stub.rectTransform.sizeDelta = (dir.x != 0 ? new Vector2(StubMeters, StubWidthMeters) : new Vector2(StubWidthMeters, StubMeters)) * _scale;
                }
                img.gameObject.SetActive(false);
                _roomViews[r] = img.gameObject;
            }
            Log.Dev($"지도 연출: 방 {_rooms.Length}, 목적지 {(_destination != null)}, 보물방 {(_treasure != null)}"); // 2인 검증용
        }

        private void TrackVisit()
        {
            var nm = NetworkManager.Singleton;
            var me = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (me == null) return;
            foreach (var r in _rooms)
                if (r != null && Inside(r, me.transform.position)) { _visited.Add(r); return; }
        }

        private void UpdateDots()
        {
            var nm = NetworkManager.Singleton;
            ulong myId = nm != null ? nm.LocalClientId : ulong.MaxValue;
            foreach (var p in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None))
            {
                ulong id = p.OwnerClientId;
                if (!_dots.TryGetValue(id, out var dot) || dot == null)
                {
                    dot = Instantiate(_dotTemplate, _content);
                    dot.gameObject.SetActive(true);
                    bool me = id == myId;
                    dot.color = me ? Color.white : PlayerVisual.ColorFor(id);
                    dot.rectTransform.sizeDelta = Vector2.one * (me ? 18f : 14f);
                    _dots[id] = dot;
                }
                dot.rectTransform.anchoredPosition = (Flat(p.transform.position) - _center) * _scale;
                if (id == myId) dot.transform.SetAsLastSibling(); // 내 점이 맨 위
            }
        }

        private static bool Inside(GridRoom r, Vector3 p)
        {
            Vector2 d = Flat(p) - Flat(r.transform.position), half = r.Size * 0.5f;
            return Mathf.Abs(d.x) <= half.x + 0.3f && Mathf.Abs(d.y) <= half.y + 0.3f;
        }

        private static Vector2 Flat(Vector3 v) => new(v.x, v.z);

#if UNITY_EDITOR
        public void EditorSetup(GameObject panel, RectTransform content, UnityEngine.UI.Image room, UnityEngine.UI.Image dot)
        { _panel = panel; _content = content; _roomTemplate = room; _dotTemplate = dot; }
#endif
    }
}
