using RatGame.Run;
using RatGame.World;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 상단 중앙 런 바 (uGUI). 기지: 출발 발판 집합·카운트다운 + 누계 / 스테이지: 출발 대기·쥐구멍 적립·누계 + "+N" + 귀환 집합·카운트다운.
    /// NetworkVariable을 읽기만 한다 — 적립 EventBus 이벤트는 호스트에서만 발생해 클라는 못 받으므로 값 변화로 감지.
    /// 소유 플레이어의 CarryHud가 생성·파괴한다. 결과 화면 중엔 숨긴다 (결과 패널은 아직 CarryHud).
    /// </summary>
    public class RunBar : MonoBehaviour
    {
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
        [SerializeField] private Color _subNormalColor = new(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color _subCountdownColor = new(0.15f, 0.45f, 0.2f, 0.85f);

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

            ShowSub($"누계 {_pad.TotalValue.Value}", false);
            SetVisible(true, false, true);
        }

        private void ShowSub(string text, bool countdown)
        {
            SetText(_subText, text);
            _subBackground.color = countdown ? _subCountdownColor : _subNormalColor;
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
