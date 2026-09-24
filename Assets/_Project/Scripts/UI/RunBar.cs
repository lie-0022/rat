using RatGame.Data;
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

            SetText(_mainText, $"쥐구멍 {run.StashedValue.Value}   누계 {run.RunTotalValue.Value}");

            bool sub = true;
            int ready = run.ReturnReadyCount.Value, needed = run.ReturnNeededCount.Value;
            if (phase == RunPhase.Returning)
                ShowSub($"<size=130%>귀환 중… {Remaining(run.ReturnAt.Value):0}</size>", true);
            else if (ready > 0) // 누가 쥐구멍에 들어가 있을 때만 — 나머지를 부르는 신호
                ShowSub($"쥐구멍에 모이면 귀환   {ready}/{needed}", false);
            else
                sub = false;

            SetVisible(true, Time.time < _popupUntil, sub);
        }

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

            ShowSub($"누계 {_pad.TotalValue.Value}   목적지 {_pad.DestinationName}", false);
            SetVisible(true, false, true);
        }

        private void ShowSub(string text, bool countdown)
        {
            SetText(_subText, text);
            if (_theme == null) return;
            Color color = _theme.GetColor(countdown ? UiColorRole.Positive : UiColorRole.Dim);
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
