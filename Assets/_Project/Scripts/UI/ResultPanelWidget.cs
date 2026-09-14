using RatGame.Data;
using RatGame.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 귀환·전멸 결과 패널 (uGUI, docs/12 ResultScreen의 1차 — 새 디자인은 와이어프레임 단계에서).
    /// RunManager NetworkVariable만 읽는다. 제목 띠 색은 테마 역할(귀환 Positive / 전멸 Danger).
    /// 스크립트는 항상 켜진 부모에 두고 패널 오브젝트만 켜고 끈다.
    /// </summary>
    public class ResultPanelWidget : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private GameObject _panel;
        [SerializeField] private UnityEngine.UI.Image _titleBar;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _line1;
        [SerializeField] private TMP_Text _line2;
        [SerializeField] private TMP_Text _line3;
        [SerializeField] private TMP_Text _countdown;

        private void Update()
        {
            var run = RunManager.Instance;
            bool show = run != null && run.IsShowingResult;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            bool returned = run.Phase.Value == RunPhase.Returned;
            if (_theme != null)
            {
                Color bar = _theme.GetColor(returned ? UiColorRole.Positive : UiColorRole.Danger);
                if (_titleBar.color != bar) _titleBar.color = bar;
            }

            int stashed = run.StashedValue.Value, total = run.RunTotalValue.Value;
            if (returned)
            {
                int carried = run.ResultCarriedValue.Value;
                SetText(_title, "귀환!");
                SetText(_line1, $"쥐구멍 {stashed}  +  들고 온 것 {carried}");
                SetText(_line2, $"이번 수확  {stashed + carried}");
                SetText(_line3, $"누계  {total}");
            }
            else
            {
                SetText(_title, "전멸…");
                SetText(_line1, "이번 파밍분을 잃었다");
                SetText(_line2, $"누계  {total}  (유지)");
                SetText(_line3, $"잃은 쥐구멍 적립  {stashed}");
            }

            double remain = System.Math.Max(0.0, run.ResultEndsAt.Value - NetworkManager.Singleton.ServerTime.Time);
            SetText(_countdown, $"{System.Math.Ceiling(remain):0}초 뒤 기지로");
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
