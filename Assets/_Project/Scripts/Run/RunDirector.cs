using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using UnityEngine;

namespace RatGame.Run
{
    public enum DirectorMode : byte { Calm, Buildup, Relief, Finale }

    /// <summary>
    /// 경계도 디렉터 (design/cat-ideas/12, 2026-09-24). 호스트 전용 — RunManager가 서버 스폰 때 런타임에 붙인다(프리팹·싱글톤 추가 없음).
    /// 긴장(최근 사건 합, 초당 감쇠)을 재고 10s마다 모드를 정한다: 잘 풀리면 Build-up, 몰리면 Relief, 귀환 카운트다운은 Finale.
    /// 고양이 감각 수치는 절대 안 건드린다 — CatBrain이 모드를 읽어 잠·머무름·순찰 목적지만 바꾼다(관찰로 알 수 있는 것만).
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        private BalanceConfigSO _balance;
        private RunManager _run;
        private float _nextDecide;
        private float _modeUntil;
        private float _lastCrisisAt;

        public float Tension { get; private set; }
        public DirectorMode Mode { get; private set; } = DirectorMode.Calm;
        public float LastCrisisAt => _lastCrisisAt;

        public void Init(BalanceConfigSO balance, RunManager run)
        {
            _balance = balance;
            _run = run;
            _lastCrisisAt = Time.time;
            _nextDecide = Time.time + _balance.DirectorTickSeconds;
        }

        private void OnEnable()
        {
            CatBrain.ServerStateChanged += OnCatState;
            EventBus.PlayerDowned += OnDowned;
            NoiseSystem.OnNoise += OnNoise;
        }

        private void OnDisable()
        {
            CatBrain.ServerStateChanged -= OnCatState;
            EventBus.PlayerDowned -= OnDowned;
            NoiseSystem.OnNoise -= OnNoise;
        }

        /// <summary>긴장 가산 (테스트·다른 시스템용). crisis면 "마지막 위기" 시각 갱신.</summary>
        public void Report(float amount, string why, bool crisis = false)
        {
            if (_balance == null) return;
            Tension = Mathf.Clamp(Tension + amount, 0f, 100f);
            if (crisis) _lastCrisisAt = Time.time;
        }

        private void OnCatState(CatBrain cat, CatState prev, CatState next)
        {
            if (_balance == null) return;
            switch (next)
            {
                case CatState.Suspicious: Report(_balance.TensionSuspicious, "의심"); break;
                case CatState.Chase: Report(_balance.TensionChase, "추격", true); break;
                case CatState.Toy: Report(_balance.TensionToy, "놀이", true); break;
            }
        }

        private void OnDowned(ulong clientId) { if (_balance != null) Report(_balance.TensionDown, "다운", true); }

        private void OnNoise(NoiseEvent e)
        {
            if (_balance != null && e.Loudness >= _balance.TensionNoiseMin) Report(_balance.TensionNoise, "소음");
        }

        private void Update()
        {
            if (_balance == null) return;
            Tension = Mathf.Max(0f, Tension - _balance.TensionDecayPerSec * Time.deltaTime);
            if (Time.time < _nextDecide) return;
            _nextDecide = Time.time + _balance.DirectorTickSeconds;
            Decide();
        }

        /// <summary>모드 판단 (10s마다, 테스트에서 즉시 호출 가능).</summary>
        public void Decide()
        {
            DirectorMode next;
            if (_run != null && _run.Phase.Value == RunPhase.Returning) next = DirectorMode.Finale;
            else if (Mode == DirectorMode.Relief && Time.time < _modeUntil) next = DirectorMode.Relief;
            else if (Tension >= _balance.ReliefThreshold)
            {
                next = DirectorMode.Relief;
                var r = _balance.ReliefSecondsRange;
                _modeUntil = Time.time + Random.Range(r.x, r.y);
            }
            else if (Tension < _balance.BuildupThreshold && Time.time - _lastCrisisAt >= _balance.BuildupQuietSeconds) next = DirectorMode.Buildup;
            else next = DirectorMode.Calm;

            if (next == Mode) return;
            Log.Dev($"디렉터: {Mode} → {next} (긴장 {Tension:0}, 마지막 위기 {Time.time - _lastCrisisAt:0}s 전)");
            Mode = next;
        }
    }
}
