using RatGame.Core;
using System.Collections.Generic;
using RatGame.Player;
using RatGame.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 좌상단 팀 상태 (docs/12 TeamStatus + 기지 로비 인원). 스폰된 플레이어의 PlayerCondition(NetworkVariable)을 읽기만 한다.
    /// 기지: "기지 · n/4명" + 초대 안내 (Steam 초대 버튼은 Facepunch 연결 때), 스테이지: "팀 n/4". 결과 화면 중엔 숨김.
    /// 인원이 최대 4라 0.25s마다 찾아도 싸다 — 매 프레임 찾지 않는다.
    /// </summary>
    public class TeamStatusWidget : MonoBehaviour
    {
        private const float PollSeconds = 0.25f;
        private const int MaxPlayers = 4; // docs/00 — 구조 상수

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private Transform _rowsParent;
        [SerializeField] private TeamStatusRow _rowTemplate;

        private readonly List<TeamStatusRow> _rows = new();
        private readonly List<PlayerCondition> _players = new();
        private float _nextPoll;
        private string _lastLogged;

        private void Awake() => _rowTemplate.gameObject.SetActive(false);

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            bool inSession = nm != null && nm.IsListening;
            bool result = RunManager.Instance != null && RunManager.Instance.IsShowingResult;
            bool show = inSession && !result;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;

            _players.Clear();
            _players.AddRange(FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None));
            _players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

            bool inStage = RunManager.Instance != null;
            SetText(_headerText, inStage ? Loc.F("팀 {0}/{1}", _players.Count, MaxPlayers) : Loc.F("기지 · {0}/{1}명", _players.Count, MaxPlayers));
            var launcher = Net.NetworkLauncher.Instance;
            string hint = inStage ? ""
                : launcher != null && launcher.UsingSteam ? Loc.T("친구 초대: Steam 친구 목록에서")
                : Loc.T("로컬 모드 — 같은 PC에서만 참가");
            SetText(_hintText, hint);
            if (_hintText.gameObject.activeSelf != (hint.Length > 0)) _hintText.gameObject.SetActive(hint.Length > 0);

            while (_rows.Count < _players.Count)
                _rows.Add(Instantiate(_rowTemplate, _rowsParent));
            for (int i = 0; i < _rows.Count; i++)
            {
                bool active = i < _players.Count;
                if (_rows[i].gameObject.activeSelf != active) _rows[i].gameObject.SetActive(active);
                if (!active) continue;
                var p = _players[i];
                string name = Loc.T(PlayerVisual.ColorNameFor(p.OwnerClientId)) + (p.IsOwner ? Loc.T(" (나)") : "");
                _rows[i].Show(PlayerVisual.ColorFor(p.OwnerClientId), name, p.State.Value, IsGrudged(p.OwnerClientId));
            }
            LogIfChanged();
        }

        // 어느 고양이든 이 쥐를 찍었으면 (CatBrain NV 읽기만 — 규칙 3). 0.25s 폴링 안에서만 호출
        private static bool IsGrudged(ulong clientId)
        {
            foreach (var cat in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None))
                if (cat.IsSpawned && cat.HasGrudge.Value && cat.GrudgeClientId.Value == clientId) return true;
            return false;
        }

        // 다인 검증용 — 보이는 내용이 바뀔 때만 (고양이 133)
        private void LogIfChanged()
        {
            var sb = new System.Text.StringBuilder(_headerText.text);
            foreach (var p in _players) sb.Append(" | ").Append(p.OwnerClientId).Append(' ').Append(p.State.Value);
            string now = sb.ToString();
            if (now == _lastLogged) return;
            _lastLogged = now;
            Core.Log.Dev($"팀 상태 연출: {now}");
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
