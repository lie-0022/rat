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
    public partial class ToastWidget : MonoBehaviour
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
        private void OnEnable() { EventBus.AchievementUnlocked += OnAchievementUnlocked; EventBus.CodexUnlocked += OnCodexUnlocked; EventBus.HouseEvent += OnHouseEvent; EventBus.CatCue += OnCatCue; EventBus.CheeseEaten += OnCheeseEaten; EventBus.StageBriefing += OnStageBriefing; EventBus.TrapBait += OnTrapBait; EventBus.TrapBaitNear += OnTrapBaitNear; EventBus.CatBelled += OnCatBelled; EventBus.RatSqueak += OnRatSqueak; EventBus.SniffCooldown += OnSniffCooldown; }
        private void OnDisable() { EventBus.AchievementUnlocked -= OnAchievementUnlocked; EventBus.CodexUnlocked -= OnCodexUnlocked; EventBus.HouseEvent -= OnHouseEvent; EventBus.CatCue -= OnCatCue; EventBus.CheeseEaten -= OnCheeseEaten; EventBus.StageBriefing -= OnStageBriefing; EventBus.TrapBait -= OnTrapBait; EventBus.TrapBaitNear -= OnTrapBaitNear; EventBus.CatBelled -= OnCatBelled; EventBus.RatSqueak -= OnRatSqueak; EventBus.SniffCooldown -= OnSniffCooldown; }

        private const int OneLineMaxChars = 40;
        private const float BriefingSeconds = 6f; // 읽을 게 많아서 평소 토스트보다 길게

        // 벽 속 스테이지 안내 (고양이 81·84) — 토스트가 최대 3개라 머리줄 + 고양이 줄 + 지도 줄
        private void OnStageBriefing(int stage, int stages, int quota, byte flags, byte modifier)
        {
            string today = Loc.T(ModifierText((Run.StageModifier)modifier));
            Show(Loc.F("스테이지 {0}/{1} — 식량 {2} 모아 목적지(주황 방)로", stage, stages, quota) + (today != null ? Loc.F("\n오늘: {0}", today) : ""), UiColorRole.Accent, BriefingSeconds);
            // 고양이 줄 — 이 스테이지에 있는 역할만 이어서 (토스트 칸 두 줄 안)
            var cats = new List<string>();
            if ((flags & Run.GridZoneBuilder.BriefGuard) != 0) cats.Add(Loc.T("문지기(푸른 회색)가 목적지 앞"));
            if ((flags & Run.GridZoneBuilder.BriefPatroller) != 0) cats.Add(Loc.T("순찰꾼(적갈색)이 큰길을 오감"));
            if ((flags & Run.GridZoneBuilder.BriefKitten) != 0) cats.Add(Loc.T("아기는 들키면 엄마를 부름"));
            if (cats.Count > 0) Show(string.Join(" · ", cats), UiColorRole.Warning, BriefingSeconds);
            // 토스트 칸은 두 줄 높이라 한 토스트에 한 문장 (스테이지 1엔 고양이 줄이 없어 3개 안에 든다)
            if ((flags & Run.GridZoneBuilder.BriefTreasure) != 0) Show(Loc.T("막다른 방 하나는 보물방 — 비싼 음식, 함정 가득"), UiColorRole.Secondary, BriefingSeconds);
            if (stage == 1) Show(((flags & Run.GridZoneBuilder.BriefPipe) != 0 ? Loc.T("회색 배관은 쥐만 · ") : "") + Loc.T("R 킁킁 = 목적지·음식 냄새 · Tab 지도"), UiColorRole.Secondary, BriefingSeconds); // 조작 팁은 배관 없는 맵에도
        }

        // 오늘의 집 (고양이 106) — 머리줄에 붙여 토스트 수를 안 늘린다
        private static string ModifierText(Run.StageModifier m) => m switch
        {
            Run.StageModifier.Blackout => "정전 (어두운 방이 많아요)",
            Run.StageModifier.TrapSale => "덫 대방출 (함정이 많아요)",
            Run.StageModifier.CatTreats => "고양이 간식 날 (고양이가 느릿느릿)",
            Run.StageModifier.OwnerOut => "집주인 외출 (집주인 사건 없음)",
            Run.StageModifier.Busy => "분주한 집 (집주인 사건이 잦아요)",
            Run.StageModifier.Guest => "고양이 손님 (한 마리 더 왔어요)",
            _ => null,
        };

        private void OnCheeseEaten(float amount) => Show(Loc.F("냠냠 — 스태미나 +{0:0}", amount), UiColorRole.Positive);
        private void OnCatBelled(Vector3 _) => Show(Loc.T("방울 달았다! Tab 지도에 고양이가 보여요"), UiColorRole.Positive);
        private void OnTrapBaitNear() => Show(Loc.T("쥐덫 위 음식은 미끼 — 웅크리고(Ctrl) 집기"), UiColorRole.Warning);
        private void OnTrapBait(bool sneaky) =>
            Show(Loc.T(sneaky ? "살금살금 — 미끼만 쏙!" : "탁! 미끼는 웅크리고(Ctrl) 살금살금"), sneaky ? UiColorRole.Positive : UiColorRole.Danger);

        // 집주인 이벤트 (design/cat-ideas/10) — 인간은 안 보인다, 소리·말로만
        private void OnHouseEvent(HouseEventKind kind, HouseEventPhase phase)
        {
            string text = (kind, phase) switch
            {
                (HouseEventKind.CallAway, HouseEventPhase.Warn) => "멀리서 \"나비야~ 간식!\"",
                (HouseEventKind.CallAway, HouseEventPhase.Start) => "고양이가 나갔다! 지금이야",
                (HouseEventKind.CallAway, HouseEventPhase.End) => "고양이가 돌아왔다… 어느 문으로?",
                (HouseEventKind.Doorbell, HouseEventPhase.Warn) => "딩동! 누가 초인종을…",
                (HouseEventKind.Doorbell, HouseEventPhase.Start) => "집주인이 현관으로 — 고양이도 따라갔다!",
                (HouseEventKind.Window, HouseEventPhase.Warn) => "덜컹 — 집주인이 창문을 연다",
                (HouseEventKind.Window, HouseEventPhase.Start) => "바람! 냄새가 날아가고 가벼운 물건이 굴러간다",
                (HouseEventKind.Window, HouseEventPhase.End) => "창문 닫힘",
                (HouseEventKind.LightOn, HouseEventPhase.Warn) => "쿵쿵 — 집주인 발소리, 어두운 방에 불이 켜진다",
                (HouseEventKind.LightOn, HouseEventPhase.Start) => "딸깍 — 불 켜짐! 어둠이 사라졌다",
                (HouseEventKind.LightOn, HouseEventPhase.End) => "불 꺼짐 — 다시 어둠",
                (HouseEventKind.TV, HouseEventPhase.Warn) => "리모컨 딸깍 — TV가 켜졌다",
                (HouseEventKind.TV, HouseEventPhase.Start) => "고양이가 TV 앞에 앉았다 — 등 뒤로 지나가",
                (HouseEventKind.TV, HouseEventPhase.End) => "TV 꺼짐",
                (HouseEventKind.Vacuum, HouseEventPhase.Warn) => "삐— 로봇청소기가 켜졌다",
                (HouseEventKind.Vacuum, HouseEventPhase.Start) => "청소기 소리에 발소리가 묻힌다 — 지금 뛰어!",
                (HouseEventKind.Vacuum, HouseEventPhase.End) => "청소기 멈춤 — 다시 조용히",
                (HouseEventKind.Flush, HouseEventPhase.Warn) => "벽 속에서 우르릉 — 배관에 물이 내려온다",
                (HouseEventKind.Flush, HouseEventPhase.Start) => "쏴아— 배관 안이면 흠뻑 젖는다 (물소리에 발소리가 묻힘)",
                (HouseEventKind.Flush, HouseEventPhase.End) => "물소리 멈춤",
                (HouseEventKind.NewTraps, HouseEventPhase.Warn) => "벽 너머 딸깍딸깍 — 집주인이 덫을 놓는다",
                (HouseEventKind.NewTraps, HouseEventPhase.Start) => "새 덫이 생겼다 — 지나온 길도 조심",
                (HouseEventKind.Feeding, HouseEventPhase.Warn) => "부엌에서 그릇 달그락 — 밥 시간",
                (HouseEventKind.Feeding, HouseEventPhase.Start) => "고양이가 밥 먹는 중 — 부엌만 피해",
                _ => null
            };
            if (text == null) return;
            Log.Dev($"집주인 알림: {text}");
            Show(Loc.T(text), phase == HouseEventPhase.Start ? UiColorRole.Positive : UiColorRole.Warning);
        }

        // 도전과제 달성 (docs/11, 고양이 219) — 제목은 한국어 원문으로 와서 여기서 번역
        private void OnAchievementUnlocked(string id, string title) => Show(Loc.F("도전과제 달성!  {0}", Loc.T(title)), UiColorRole.Accent);

        private void OnCodexUnlocked(string itemId)
        {
            var item = _database != null ? _database.GetById(itemId) : null;
            Show(Loc.F("도감 등록!  {0}", item != null ? Loc.T(item.DisplayName) : itemId), UiColorRole.Accent);
        }

        public void Show(string text, UiColorRole stripe) => Show(text, stripe, _seconds);

        public void Show(string text, UiColorRole stripe, float seconds)
        {
            if (_live.Count >= _maxToasts) Remove(0);
            var go = Instantiate(_toastTemplate, _stack);
            var label = go.GetComponentInChildren<TMP_Text>();
            label.text = text;
            // 짧은 건 한 줄(자동 크기로 살짝 줄여서 — 방향 붙은 자막이 "뒤"만 꺾이던 것), 긴 안내(고양이 역할 셋)는 예전처럼 두 줄 (고양이 153·154)
            label.textWrappingMode = text.Length > OneLineMaxChars ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            var strip = go.transform.Find("Stripe");
            if (strip != null && _theme != null) strip.GetComponent<UnityEngine.UI.Image>().color = _theme.GetColor(stripe);
            go.SetActive(true);
            _live.Add(new Live { Go = go, Group = go.GetComponent<CanvasGroup>(), EndsAt = Time.unscaledTime + seconds });
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
            PollQuota();
        }

        // 할당량을 처음 채운 순간 한 번 (고양이 95) — 판정은 이미 NV(적립·할당량)라 읽기만. 스테이지가 바뀌면(새 StageQuota) 다시
        private Run.StageQuota _quotaSeen;
        private bool _quotaWasMet;

        private void PollQuota()
        {
            var run = Run.RunManager.Instance;
            var quota = run != null ? run.GetComponent<Run.StageQuota>() : null;
            if (quota == null || !quota.IsSpawned || quota.Quota.Value <= 0) { _quotaSeen = null; return; }
            bool met = quota.Met(run.StashedValue.Value);
            if (quota != _quotaSeen) { _quotaSeen = quota; _quotaWasMet = met; return; } // 늦게 들어와 이미 채운 상태면 알리지 않음
            if (met && !_quotaWasMet) Show(Loc.T("할당량 채움! 이제 목적지에 모두 모이면 다음 스테이지"), UiColorRole.Accent);
            _quotaWasMet = met;
        }

        private static bool IsWalls => Run.RunManager.Instance != null && Run.RunManager.Instance.GetComponent<Run.StageQuota>() != null; // 벽 속은 목적지 창고 (고양이 96)

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
                string name = Loc.T(PlayerVisual.ColorNameFor(id));
                if (!_known.TryGetValue(id, out var prev))
                {
                    if (_snapshotted && !p.IsOwner) Show(Loc.F("{0}가 들어왔어요", name), UiColorRole.Positive);
                }
                else if (prev != state && p.IsOwner)
                {
                    // 고양이가 놓아줬다 — 도망칠 창 (design/cat-ideas/04)
                    if (prev == ConditionState.Pinned && state == ConditionState.Active) Show(Loc.T("풀려났다! 지금 도망쳐!"), UiColorRole.Warning);
                    if (state == ConditionState.Trapped) Show(Loc.T("끈끈이! 동료가 E로 구해 줘야 해요"), UiColorRole.Danger);
                    if (state == ConditionState.Stunned) Show(Loc.T("찌릿! 잠깐 못 움직여요"), UiColorRole.Warning);
                    if (state == ConditionState.Pinned) Show(Loc.T("잡혔다! A·D 번갈아 연타 = 버둥 — 보는 쪽으로 굴러간다"), UiColorRole.Danger);
                    // 쓰러지면 몸이 대리 몸으로 바뀌고 조작이 멈춘다 — 기다리면 된다는 것과 살아나는 길을 본인에게도 (고양이 183)
                    if (state == ConditionState.Downed) Show(Loc.F("쓰러졌어요… 동료가 내 몸을 {0}으로 끌고 가면 살아나요\n[Q] 찍찍 = 동료에게 내 자리 알림 (고양이도 들어요)", Loc.T(IsWalls ? "창고(주황 방)" : "쥐구멍")), UiColorRole.Danger); // 둘째 줄 고양이 184
                    if (prev == ConditionState.Downed) Show(Loc.T("살아났다! 다시 움직여요"), UiColorRole.Positive);
                }
                else if (prev != state && !p.IsOwner)
                {
                    // 둘째 줄 = 어디 (고양이 155) — 넓어진 맵에서 지도를 안 열어도 첫 발을 뗄 수 있게
                    if (state == ConditionState.Pinned) Show(Loc.F("{0}가 고양이에게 잡혔어요! 눈에 띄어 고양이를 떼어 내요\n{1}", name, Where(p.transform.position)), UiColorRole.Danger);
                    if (state == ConditionState.Downed) Show(Loc.F("{0}가 다운됐어요! {1}으로 옮겨 주세요\n{2}", name, Loc.T(IsWalls ? "창고(주황 방)" : "쥐구멍"), Where(p.transform.position)), UiColorRole.Danger);
                    else if (state == ConditionState.Trapped) Show(Loc.F("{0}가 끈끈이에 붙었어요! [E] 길게 눌러 구출\n{1}", name, Where(p.transform.position)), UiColorRole.Warning);
                    if (state == ConditionState.Pinned || state == ConditionState.Downed || state == ConditionState.Trapped) Log.Dev($"동료 위기 알림: {name} {state} — {Where(p.transform.position)}"); // 2인 검증용
                }
                _known[id] = state;
            }

            _gone.Clear();
            foreach (var id in _known.Keys)
                if (!_seen.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
            {
                _known.Remove(id);
                if (_snapshotted) Show(Loc.F("{0}가 나갔어요", Loc.T(PlayerVisual.ColorNameFor(id))), UiColorRole.Secondary);
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
