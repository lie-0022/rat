using RatGame.Core;
using System.Collections.Generic;
using RatGame.Data;
using RatGame.Player;
using RatGame.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 귀환·전멸 결과 화면 (docs/12 ResultScreen, 2026-09-16 개편). 큰 제목 띠 + 수확 수치 + 플레이어별 기여("오늘의 일꾼" 왕관)
    /// + 이번 런에 새로 채운 도감 칩 + 기지까지 남은 시간 막대. RunManager NetworkVariable·Contributions와
    /// 내 PlayerCodex(로컬)만 읽는다 (규칙 3). 스크립트는 항상 켜진 부모에 두고 패널만 켜고 끈다.
    /// </summary>
    public class ResultPanelWidget : MonoBehaviour
    {
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private GameObject _panel;
        [Header("제목")]
        [SerializeField] private UnityEngine.UI.Image _titleBand;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [Header("수확")]
        [SerializeField] private TMP_Text _stashedText;
        [SerializeField] private TMP_Text _carriedText;
        [SerializeField] private TMP_Text _haulLabel;
        [SerializeField] private TMP_Text _haulText;
        [SerializeField] private TMP_Text _totalText;
        [Header("오늘의 쥐들")]
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private ResultPlayerRow _rowTemplate;
        [Header("새 도감")]
        [SerializeField] private Transform _chipsParent;
        [SerializeField] private GameObject _chipTemplate;
        [SerializeField] private TMP_Text _noChipsText;
        [Header("남은 시간")]
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private RectTransform _countdownFill;

        private readonly List<ResultPlayerRow> _rows = new();
        private readonly List<GameObject> _chips = new();
        private int _chipSeed = -1;

        private void Awake()
        {
            _rowTemplate.gameObject.SetActive(false);
            _chipTemplate.SetActive(false);
        }

        private void Update()
        {
            var run = RunManager.Instance;
            bool show = run != null && run.IsShowingResult;
            if (_panel.activeSelf != show)
            {
                _panel.SetActive(show);
                _chipSeed = -1; // 새 결과마다 칩 다시
            }
            if (!show) return;

            bool returned = run.Phase.Value == RunPhase.Returned;
            var quota = run.GetComponent<StageQuota>(); // 새 루프(벽 속) — 클리어·엔딩·굶음 문구
            if (quota != null) RefreshQuotaTitle(quota, returned); else RefreshTitle(returned);
            RefreshHarvest(run, returned);
            RefreshRows(run, returned);
            RefreshChips(run);
            RefreshCountdown(run, quota != null && returned && !quota.Finished.Value ? Loc.T("다음 맵으로") : Loc.T("기지로"));
        }

        private void RefreshQuotaTitle(StageQuota quota, bool cleared)
        {
            SetColor(_titleBand, cleared ? UiColorRole.Positive : UiColorRole.Danger);
            if (!cleared)
            {
                SetText(_title, Loc.T("굶었다..."));
                SetText(_subtitle, Loc.F("스테이지 {0}에서 전원 쓰러짐 — 처음부터 다시", quota.StageNumber.Value));
            }
            else if (quota.Finished.Value)
            {
                // 엔딩 (docs/09 새 루프 — 짧은 이야기, 스토리는 기획이 채운다)
                SetText(_title, Loc.T("배불리 겨울을 났다!"));
                SetText(_subtitle, Loc.F("스테이지 {0}개를 모두 넘어 가족이 굶지 않았어요 — 엔딩\n이번 런: 모은 식량 {1} · 상점에서 산 물건 {2}개", quota.StagesPerRun.Value, quota.RunHaul.Value, quota.RunBuys.Value)); // 요약 (고양이 108)
            }
            else
            {
                SetText(_title, Loc.F("스테이지 {0} 클리어!", quota.StageNumber.Value));
                SetText(_subtitle, Loc.F("가족이 {0}만큼 먹었어요 — 남은 식량 {1}", quota.Quota.Value, quota.Pantry.Value));
            }
        }

        private void RefreshTitle(bool returned)
        {
            SetColor(_titleBand, returned ? UiColorRole.Positive : UiColorRole.Danger);
            SetText(_title, Loc.T(returned ? "무사히 이사 완료!" : "고양이 밥이 되었다..."));
            SetText(_subtitle, Loc.T(returned ? "전원 쥐구멍 집합 — 오늘 수확을 기지로 옮겼어요" : "전원 다운 — 이번 적립을 잃었어요 (누계는 그대로)"));
        }

        private void RefreshHarvest(RunManager run, bool returned)
        {
            int stashed = run.StashedValue.Value, carried = run.ResultCarriedValue.Value;
            SetText(_stashedText, stashed.ToString());
            // 줄 이름표 — 벽 속은 목적지 창고 (고양이 96 말투와 같게, 프리팹 글자는 창고 맵 기준)
            var label = _stashedText.transform.parent.Find("Label");
            if (label != null) SetText(label.GetComponent<TMP_Text>(), Loc.T(run.GetComponent<StageQuota>() != null ? "창고 적립" : "쥐구멍 적립"));
            SetText(_carriedText, returned ? carried.ToString() : "—");
            SetText(_haulLabel, Loc.T(returned ? "이번 수확" : "잃은 적립"));
            SetText(_haulText, returned ? $"+{stashed + carried}" : $"-{stashed}");
            SetColor(_haulText, returned ? UiColorRole.AccentText : UiColorRole.DangerText);
            SetText(_totalText, run.RunTotalValue.Value.ToString());
        }

        private void RefreshRows(RunManager run, bool returned)
        {
            var list = run.Contributions;
            while (_rows.Count < list.Count)
            {
                var row = Instantiate(_rowTemplate, _rowsParent);
                row.gameObject.SetActive(true);
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].gameObject.activeSelf != i < list.Count) _rows[i].gameObject.SetActive(i < list.Count);

            // 일꾼 = 가장 많이 기여한 한 명 (귀환은 적립+들고 옴, 전멸은 적립만). 0이면 없음
            int best = -1, bestValue = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int value = returned ? list[i].Total : list[i].DepositedValue;
                if (value > bestValue) { best = i; bestValue = value; }
            }

            var nm = NetworkManager.Singleton;
            string depositLabel = Loc.T(run.GetComponent<StageQuota>() != null ? "창고" : "쥐구멍"); // 벽 속은 목적지 창고 (고양이 96)
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                bool me = nm != null && c.ClientId == nm.LocalClientId;
                // 이름·색은 팀 기본색 기준 (팀 상태·토스트와 같은 이름, 나간 쥐도 표시 가능)
                // 구조 횟수는 이름 옆에 (고양이 186) — 식량을 못 모았어도 판을 살린 쥐가 보이게
                string rescues = c.Rescues > 0 ? Loc.F(" · 구조 {0}", c.Rescues) : "";
                _rows[i].Show($"{Loc.T(PlayerVisual.ColorNameFor(c.ClientId))}{(me ? Loc.T(" (나)") : "")}{rescues}", PlayerVisual.ColorFor(c.ClientId),
                              c.DepositedValue, c.DepositCount, c.CarriedValue, i == best, c.Downed && returned, returned, depositLabel);
            }
        }

        private void RefreshChips(RunManager run)
        {
            var nm = NetworkManager.Singleton;
            var codex = nm != null && nm.LocalClient?.PlayerObject != null ? nm.LocalClient.PlayerObject.GetComponent<PlayerCodex>() : null;
            IReadOnlyList<string> ids = codex != null ? codex.NewUnlocksFor(run.RunSeed.Value) : System.Array.Empty<string>();
            if (_chipSeed == run.RunSeed.Value && _chips.Count == ids.Count) return;

            _chipSeed = run.RunSeed.Value;
            foreach (var chip in _chips) Destroy(chip);
            _chips.Clear();
            foreach (string id in ids)
            {
                var chip = Instantiate(_chipTemplate, _chipsParent);
                var item = _database != null ? _database.GetById(id) : null;
                chip.GetComponentInChildren<TMP_Text>().text = item != null ? Loc.T(item.DisplayName) : id;
                chip.SetActive(true);
                _chips.Add(chip);
            }
            _noChipsText.gameObject.SetActive(ids.Count == 0);
        }

        private void RefreshCountdown(RunManager run, string where)
        {
            double remain = System.Math.Max(0.0, run.ResultEndsAt.Value - NetworkManager.Singleton.ServerTime.Time);
            SetText(_countdownText, Loc.F("{0:0}초 뒤 {1}", System.Math.Ceiling(remain), where));
            float duration = _balance != null ? _balance.ResultScreenSeconds : 8f;
            float t = Mathf.Clamp01((float)(remain / duration));
            if (!Mathf.Approximately(_countdownFill.anchorMax.x, t)) _countdownFill.anchorMax = new Vector2(t, 1f);
        }

        private void SetColor(UnityEngine.UI.Graphic graphic, UiColorRole role)
        {
            if (_theme == null) return;
            Color color = _theme.GetColor(role);
            if (graphic.color != color) graphic.color = color;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
