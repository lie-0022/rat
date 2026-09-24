using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 집주인 이벤트 스케줄러 (design/cat-ideas/10, 2026-09-24). 호스트 전용 — RunManager가 서버 스폰 때 런타임에 붙인다.
    /// 스테이지당 2~4개, 첫 사건 90~150s, 간격 180~300s, 같은 사건 연속 금지. 사건마다 예고 3s → 본 사건.
    /// 경계도 디렉터가 Relief(팀이 몰림)면 부르기를 앞당긴다 — 구출·대이동 창.
    /// 인간은 안 보인다: 알림은 RunManager ClientRpc → EventBus → 토스트.
    /// </summary>
    public class HouseEventDirector : MonoBehaviour
    {
        private BalanceConfigSO _balance;
        private RunManager _run;
        private int _remaining;
        private float _nextAt;
        private float _lastEventAt = -999f;
        private HouseEventKind? _last;
        private HouseEventKind? _pending;
        private float _pendingStartAt;
        private bool _scheduled;

        public int Remaining => _remaining;
        public float NextAt => _nextAt;

        public void Init(BalanceConfigSO balance, RunManager run)
        {
            _balance = balance;
            _run = run;
        }

        private void OnEnable() => CatBrain.ServerStateChanged += OnCatState;
        private void OnDisable() => CatBrain.ServerStateChanged -= OnCatState;

        private void OnCatState(CatBrain cat, CatState prev, CatState next)
        {
            if (prev == CatState.Away && _run != null) _run.ServerHouseEvent(HouseEventKind.CallAway, HouseEventPhase.End); // 초인종도 같은 복귀 문구
        }

        private void Update()
        {
            if (_balance == null || _run == null) return;

            if (_pending.HasValue && Time.time >= _pendingStartAt)
            {
                var kind = _pending.Value;
                _pending = null;
                StartEvent(kind);
            }

            // 스테이지가 시작되면 일정 잡기 (에디터 샌드박스(Ready)에선 수동 트리거만)
            if (_run.Phase.Value != RunPhase.StageActive) return;
            if (!_scheduled)
            {
                _scheduled = true;
                var n = _balance.HouseEventsPerStage;
                _remaining = Random.Range(n.x, n.y + 1);
                _nextAt = Time.time + Random.Range(_balance.HouseEventFirstDelay.x, _balance.HouseEventFirstDelay.y);
                Log.Dev($"집주인: 이번 스테이지 {_remaining}건, 첫 사건 {_nextAt - Time.time:0}s 뒤");
            }
            if (_remaining <= 0 || _pending.HasValue) return;

            var director = GetComponent<RunDirector>();
            bool relief = director != null && director.Mode == DirectorMode.Relief && Time.time - _lastEventAt >= _balance.ReliefCallAwayMinGap;
            if (relief) { Log.Dev("집주인: 디렉터 Relief — 부르기 앞당김"); Queue(HouseEventKind.CallAway); return; }
            if (Time.time >= _nextAt) Queue(Pick());
        }

        // 불 켜짐: 지금 어두운 구역 중 무작위 하나
        private static LightZone PickDarkZone()
        {
            var dark = new List<LightZone>();
            foreach (var z in LightZone.All) if (z.IsSpawned && !z.Lit.Value) dark.Add(z);
            return dark.Count == 0 ? null : dark[Random.Range(0, dark.Count)];
        }

        private HouseEventKind Pick()
        {
            // 같은 사건 연속 금지 — 부르기·밥·청소기 중 직전 것 빼고 무작위
            var pool = new System.Collections.Generic.List<HouseEventKind> { HouseEventKind.CallAway, HouseEventKind.Feeding };
            if (FindAnyObjectByType<RobotVacuum>() != null) pool.Add(HouseEventKind.Vacuum);
            if (FindAnyObjectByType<TvSet>() != null) pool.Add(HouseEventKind.TV);
            if (LightZone.All.Count > 0) pool.Add(HouseEventKind.LightOn);
            if (FindAnyObjectByType<WindowWind>() != null) pool.Add(HouseEventKind.Window);
            var walls = FindAnyObjectByType<GridZoneBuilder>();
            if (walls != null && walls.CanAddTraps) pool.Add(HouseEventKind.NewTraps); // 벽 속만 — 집주인이 벽 너머에 덫 (고양이 89)
            if (_last.HasValue) pool.Remove(_last.Value);
            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>호스트: 사건 예약(예고부터). 테스트·다른 시스템(초인종 등)용.</summary>
        public void ServerTrigger(HouseEventKind kind) => Queue(kind, kind != HouseEventKind.Doorbell);

        private void Queue(HouseEventKind kind, bool countsAsScheduled = true)
        {
            _pending = kind;
            _pendingStartAt = Time.time + _balance.HouseEventWarnSeconds;
            _lastEventAt = Time.time;
            if (countsAsScheduled) // 초인종(쥐가 유발)은 스케줄 몫을 안 깎는다
            {
                _remaining = Mathf.Max(0, _remaining - 1);
                _last = kind;
                _nextAt = Time.time + Random.Range(_balance.HouseEventGap.x, _balance.HouseEventGap.y);
            }
            _run.ServerHouseEvent(kind, HouseEventPhase.Warn);
            Log.Dev($"집주인: {kind} 예고 (남은 {_remaining}건)");
        }

        private void StartEvent(HouseEventKind kind)
        {
            var cats = FindObjectsByType<CatBrain>(FindObjectsSortMode.None);
            switch (kind)
            {
                case HouseEventKind.CallAway:
                case HouseEventKind.Doorbell: // 집주인이 현관으로 — 고양이도 따라간다
                    float away = Random.Range(_balance.CatCallAwaySeconds.x, _balance.CatCallAwaySeconds.y);
                    foreach (var cat in cats) cat.ServerCallAway(away);
                    break;
                case HouseEventKind.Feeding:
                    foreach (var cat in cats) cat.ServerFeedingTime(_balance.CatFeedingSeconds);
                    break;
                case HouseEventKind.TV:
                    var tv = FindAnyObjectByType<TvSet>();
                    if (tv == null) break;
                    tv.ServerStart(_balance.TvSeconds);
                    foreach (var cat in cats) cat.ServerWatchTv(tv.transform.position, tv.WatchPoint, _balance.TvSeconds);
                    break;
                case HouseEventKind.Window:
                    var window = FindAnyObjectByType<WindowWind>();
                    if (window != null) window.ServerStart(_balance.WindowSeconds);
                    break;
                case HouseEventKind.LightOn:
                    var zone = PickDarkZone();
                    if (zone == null) break;
                    zone.ServerLightFor(_balance.LightOnSeconds);
                    foreach (var cat in cats) cat.ServerGreetOwner(zone.Center, _balance.LightOnSeconds);
                    break;
                case HouseEventKind.NewTraps:
                    var builder = FindAnyObjectByType<GridZoneBuilder>();
                    if (builder != null) builder.ServerAddTraps(_balance.HouseNewTrapsCount, _balance.HouseNewTrapsAvoid);
                    break;
                case HouseEventKind.Vacuum:
                    var vac = FindAnyObjectByType<RobotVacuum>();
                    if (vac == null) break;
                    vac.ServerStart(_balance.VacuumSeconds);
                    foreach (var cat in cats) cat.ServerAvoidVacuum(vac.Position, _balance.VacuumSeconds);
                    break;
            }
            _run.ServerHouseEvent(kind, HouseEventPhase.Start);
            Log.Dev($"집주인: {kind} 시작");
        }
    }
}
