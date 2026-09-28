using RatGame.Data;
using RatGame.Player;
using RatGame.Run;
using RatGame.World;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 상단 중앙 런 바 (uGUI). 기지: 출발 발판 집합·카운트다운 + 누계 / 스테이지: 출발 대기·쥐구멍·누계 + "+N" + 귀환 집합·카운트다운.
    /// NetworkVariable을 읽기만 한다 — 적립 EventBus 이벤트는 호스트에서만 발생해 클라는 못 받으므로 값 변화로 감지.
    /// HUD(HudController) 안에 있고, 결과 화면 중엔 숨긴다. 둘째 줄 배경색은 상태에 따라 테마 역할로 바꾼다.
    /// </summary>
    public class RunBar : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;

        [Header("메인 줄")]
        [SerializeField] private GameObject _mainBox;
        [SerializeField] private TMP_Text _mainText;

        [Header("+N 팝업")]
        [SerializeField] private GameObject _popupBox;
        [SerializeField] private TMP_Text _popupText;
        [SerializeField] private float _popupSeconds = 1.5f;

        [Header("둘째 줄 (집합·카운트다운·기지 누계)")]
        [SerializeField] private GameObject _subBox;
        [SerializeField] private UnityEngine.UI.Image _subBackground;
        [SerializeField] private TMP_Text _subText;

        private RunManager _boundRun;
        private DeparturePad _pad;
        private float _padSearchAt;
        private float _popupUntil;
        private bool _emptyHanded;
        private int _downedCount;
        private float _scanAt;

        private void Update()
        {
            if (_boundRun != RunManager.Instance) BindRun(RunManager.Instance);

            if (_boundRun != null) ShowRun(_boundRun);
            else ShowPad();
        }

        private void OnDestroy() => BindRun(null);

        private void BindRun(RunManager run)
        {
            if (_boundRun != null) _boundRun.StashedValue.OnValueChanged -= OnStashedChanged;
            _boundRun = run;
            if (_boundRun != null) _boundRun.StashedValue.OnValueChanged += OnStashedChanged;
        }

        private void OnStashedChanged(int prev, int now)
        {
            if (now <= prev) return; // 출발 시 리셋(→0)은 팝업 없음
            SetText(_popupText, $"+{now - prev}");
            _popupUntil = Time.time + _popupSeconds;
        }

        private void ShowRun(RunManager run)
        {
            if (run.IsShowingResult) { SetVisible(false, false, false); return; }

            var phase = run.Phase.Value;
            if (phase == RunPhase.Ready)
            {
                bool host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
                SetText(_mainText, host
                    ? $"[Enter] 출발   누계 {run.RunTotalValue.Value}"
                    : $"호스트 출발 대기   누계 {run.RunTotalValue.Value}");
                SetVisible(true, false, false);
                return;
            }

            var quota = run.GetComponent<StageQuota>(); // 새 루프(벽 속)만 있다
            int stashed = run.StashedValue.Value;
            SetText(_mainText, quota != null
                ? $"스테이지 {quota.StageNumber.Value}/{quota.StagesPerRun.Value}   식량 {stashed}/{quota.Quota.Value}{TodayShort((StageModifier)quota.Modifier.Value)}"
                : $"쥐구멍 {stashed}   누계 {run.RunTotalValue.Value}");

            bool sub = true;
            int ready = run.ReturnReadyCount.Value, needed = run.ReturnNeededCount.Value;
            // 예전 루프는 할당량이 없어 식량 0이어도 떠난다 — 실수로 빈손 귀환하지 않게 알려만 준다 (고양이 181)
            bool gathering = ready > 0 || phase == RunPhase.Returning;
            if (gathering) Scan();
            bool empty = gathering && quota == null && stashed == 0 && _emptyHanded;
            // 집합은 쓰러진 쥐를 빼고 센다 — 그 쥐와 든 물건을 두고 떠난다는 걸 모이는 줄에 알린다 (고양이 182)
            string left = gathering && _downedCount > 0 ? $" · 쓰러진 {_downedCount}명 두고 감" : "";
            bool warn = empty || left.Length > 0;
            if (phase == RunPhase.Returning)
                ShowSub($"<size=130%>{(quota != null ? "다음으로" : "귀환 중")}… {Remaining(run.ReturnAt.Value):0}</size>{(empty ? " · 빈손" : "")}{left}",
                    warn ? UiColorRole.Warning : UiColorRole.Positive);
            else if (ready > 0 && quota != null && !quota.Met(stashed)) // 창고에 왔는데 모자람
                ShowSub($"식량이 모자라요 — {quota.Quota.Value - stashed} 더 모아 창고에", false);
            else if (ready > 0) // 누가 쥐구멍에 들어가 있을 때만 — 나머지를 부르는 신호
                ShowSub((quota != null ? $"창고에 모이면 다음으로   {ready}/{needed}" : $"쥐구멍에 모이면 귀환   {ready}/{needed}{(empty ? " · 아직 빈손이에요" : "")}") + left,
                    warn ? UiColorRole.Warning : UiColorRole.Dim);
            else if (quota != null && quota.Met(stashed)) // 채웠지만 아직 아무도 창고에 없음 — 갈지 더 모을지 (고양이 95)
                ShowSub($"할당량 채움! 창고에 모이면 다음 · 상점 돈 +{stashed - quota.Quota.Value}", false); // 짧게 — 오른쪽 토스트 칸과 안 겹치게
            else
                sub = false;

            SetVisible(true, Time.time < _popupUntil, sub);
        }

        // 오늘의 집 짧게 (고양이 120) — 자세한 건 스테이지 안내 토스트
        private static string TodayShort(StageModifier m) => m switch
        {
            StageModifier.Blackout => "   · 정전", StageModifier.TrapSale => "   · 덫 대방출", StageModifier.CatTreats => "   · 간식 날",
            StageModifier.OwnerOut => "   · 집주인 외출", StageModifier.Busy => "   · 분주한 집", StageModifier.Guest => "   · 고양이 손님",
            _ => "",
        };

        // 기지 (런 매니저가 없는 씬)
        private void ShowPad()
        {
            if (_pad == null && Time.time >= _padSearchAt)
            {
                _pad = FindFirstObjectByType<DeparturePad>();
                _padSearchAt = Time.time + 1f;
            }
            if (_pad == null || !_pad.IsSpawned) { SetVisible(false, false, false); return; }

            if (_pad.Counting.Value)
                SetText(_mainText, $"<size=130%>출발… {Remaining(_pad.DepartAt.Value):0}</size>");
            else
                SetText(_mainText, $"출발 발판에 모이면 출발   {_pad.ReadyCount.Value}/{_pad.NeededCount.Value}");

            string record = _pad.BestStage.Value > 0 ? $"   최고 스테이지 {_pad.BestStage.Value}{(_pad.Endings.Value > 0 ? $" · 엔딩 {_pad.Endings.Value}번" : "")}" : "";
            ShowSub($"누계 {_pad.TotalValue.Value}   목적지 {_pad.DestinationName}{record}", false);
            SetVisible(true, false, true);
        }

        // 0.5초마다: 누구 손·주머니에도 값 있는 물건이 없나(귀환 계산과 같은 기준) + 쓰러진 쥐 수. 동기화된 값만 읽어 클라도 같다
        private void Scan()
        {
            if (Time.time < _scanAt) return;
            _scanAt = Time.time + 0.5f;
            _emptyHanded = true;
            foreach (var carry in FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None))
            {
                if (HasValue(carry.CarriedItem)) { _emptyHanded = false; break; }
                for (int s = 0; s < carry.SlotCount && _emptyHanded; s++)
                    if (HasValue(carry.GetSlotItem(s))) _emptyHanded = false;
                if (!_emptyHanded) break;
            }
            _downedCount = 0;
            foreach (var condition in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None))
                if (condition.IsSpawned && condition.State.Value == ConditionState.Downed) _downedCount++;
        }

        private static bool HasValue(CarryableItem item) => item != null && item.EffectiveValue > 0;

        private void ShowSub(string text, bool countdown) => ShowSub(text, countdown ? UiColorRole.Positive : UiColorRole.Dim);

        private void ShowSub(string text, UiColorRole role)
        {
            SetText(_subText, text);
            if (_theme == null) return;
            Color color = _theme.GetColor(role);
            if (_subBackground.color != color) _subBackground.color = color;
        }

        private void SetVisible(bool main, bool popup, bool sub)
        {
            if (_mainBox.activeSelf != main) _mainBox.SetActive(main);
            if (_popupBox.activeSelf != popup) _popupBox.SetActive(popup);
            if (_subBox.activeSelf != sub) _subBox.SetActive(sub);
        }

        // 같은 문자열이면 건드리지 않는다 — 매 프레임 대입하면 캔버스가 계속 다시 그려짐
        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }

        private static double Remaining(double endServerTime) =>
            System.Math.Ceiling(System.Math.Max(0.0, endServerTime - NetworkManager.Singleton.ServerTime.Time));
    }
}
