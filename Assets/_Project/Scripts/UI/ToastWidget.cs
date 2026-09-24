using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 우상단 토스트 (docs/12 Toast). 알림 원천:
    ///  - 도감 등록: EventBus.CodexUnlocked (내 클라 로컬 — PlayerCodex가 발행)
    ///  - 동료 들어옴·나감, 동료 다운·끈끈이: 스폰된 PlayerCondition(NetworkVariable) 변화를 읽어서 (규칙 3 — 시스템이 UI를 부르지 않음)
    /// 최대 3개 쌓이고 오래된 것부터 밀려난다. 내 상태 변화는 화면으로 이미 보이므로 알리지 않는다.
    /// </summary>
    public class ToastWidget : MonoBehaviour
    {
        private const float PollSeconds = 0.25f;
        private const float FadeSeconds = 0.4f;

        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private Transform _stack;
        [SerializeField] private GameObject _toastTemplate;
        [SerializeField] private float _seconds = 3.5f;
        [SerializeField] private int _maxToasts = 3;

        private struct Live
        {
            public GameObject Go;
            public CanvasGroup Group;
            public float EndsAt;
        }

        private readonly List<Live> _live = new();
        private readonly Dictionary<ulong, ConditionState> _known = new();
        private readonly HashSet<ulong> _seen = new();
        private readonly List<ulong> _gone = new();
        private bool _snapshotted;
        private float _nextPoll;

        private void Awake() => _toastTemplate.SetActive(false);
        private void OnEnable() { EventBus.CodexUnlocked += OnCodexUnlocked; EventBus.HouseEvent += OnHouseEvent; }
        private void OnDisable() { EventBus.CodexUnlocked -= OnCodexUnlocked; EventBus.HouseEvent -= OnHouseEvent; }

        // 집주인 이벤트 (design/cat-ideas/10) — 인간은 안 보인다, 소리·말로만
        private void OnHouseEvent(HouseEventKind kind, HouseEventPhase phase)
        {
            string text = (kind, phase) switch
            {
                (HouseEventKind.CallAway, HouseEventPhase.Warn) => "멀리서 \"나비야~ 간식!\"",
                (HouseEventKind.CallAway, HouseEventPhase.Start) => "고양이가 나갔다! 지금이야",
                (HouseEventKind.CallAway, HouseEventPhase.End) => "고양이가 돌아왔다… 어느 문으로?",
                (HouseEventKind.Feeding, HouseEventPhase.Warn) => "부엌에서 그릇 달그락 — 밥 시간",
                (HouseEventKind.Feeding, HouseEventPhase.Start) => "고양이가 밥 먹는 중 — 부엌만 피해",
                _ => null
            };
            if (text == null) return;
            Log.Dev($"집주인 알림: {text}");
            Show(text, phase == HouseEventPhase.Start ? UiColorRole.Positive : UiColorRole.Warning);
        }

        private void OnCodexUnlocked(string itemId)
        {
            var item = _database != null ? _database.GetById(itemId) : null;
            Show($"도감 등록!  {(item != null ? item.DisplayName : itemId)}", UiColorRole.Accent);
        }

        public void Show(string text, UiColorRole stripe)
        {
            if (_live.Count >= _maxToasts) Remove(0);
            var go = Instantiate(_toastTemplate, _stack);
            go.GetComponentInChildren<TMP_Text>().text = text;
            var strip = go.transform.Find("Stripe");
            if (strip != null && _theme != null) strip.GetComponent<UnityEngine.UI.Image>().color = _theme.GetColor(stripe);
            go.SetActive(true);
            _live.Add(new Live { Go = go, Group = go.GetComponent<CanvasGroup>(), EndsAt = Time.unscaledTime + _seconds });
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                float left = _live[i].EndsAt - now;
                if (left <= 0f) { Remove(i); continue; }
                float alpha = Mathf.Clamp01(left / FadeSeconds);
                if (!Mathf.Approximately(_live[i].Group.alpha, alpha)) _live[i].Group.alpha = alpha;
            }

            if (now < _nextPoll) return;
            _nextPoll = now + PollSeconds;
            PollPlayers();
        }

        private void PollPlayers()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                _known.Clear();
                _snapshotted = false;
                return;
            }

            _seen.Clear();
            foreach (var p in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None))
            {
                ulong id = p.OwnerClientId;
                var state = p.State.Value;
                _seen.Add(id);
                string name = PlayerVisual.ColorNameFor(id);
                if (!_known.TryGetValue(id, out var prev))
                {
                    if (_snapshotted && !p.IsOwner) Show($"{name}가 들어왔어요", UiColorRole.Positive);
                }
                else if (prev != state && p.IsOwner)
                {
                    // 고양이가 놓아줬다 — 도망칠 창 (design/cat-ideas/04)
                    if (prev == ConditionState.Pinned && state == ConditionState.Active) Show("풀려났다! 지금 도망쳐!", UiColorRole.Warning);
                }
                else if (prev != state && !p.IsOwner)
                {
                    if (state == ConditionState.Pinned) Show($"{name}가 고양이에게 잡혔어요! 눈에 띄어 고양이를 떼어 내요", UiColorRole.Danger);
                    if (state == ConditionState.Downed) Show($"{name}가 다운됐어요! 쥐구멍으로 옮겨 주세요", UiColorRole.Danger);
                    else if (state == ConditionState.Trapped) Show($"{name}가 끈끈이에 붙었어요! [E] 길게 눌러 구출", UiColorRole.Warning);
                }
                _known[id] = state;
            }

            _gone.Clear();
            foreach (var id in _known.Keys)
                if (!_seen.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
            {
                _known.Remove(id);
                if (_snapshotted) Show($"{PlayerVisual.ColorNameFor(id)}가 나갔어요", UiColorRole.Secondary);
            }
            // 처음 본 목록(세션 입장·HUD 생성 직후)은 알리지 않는다
            _snapshotted = true;
        }

        private void Remove(int index)
        {
            Destroy(_live[index].Go);
            _live.RemoveAt(index);
        }
    }
}
