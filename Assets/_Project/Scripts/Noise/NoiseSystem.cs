using System;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Noise
{
    public enum NoiseType { Footstep, Impact, Break, Squeak, Trap, Item, PlayerMic }

    public struct NoiseEvent
    {
        public Vector3 Pos;
        public float Loudness;   // 0~100
        public NoiseType Type;
        public ulong Source;     // 발생시킨 클라 (HasSource일 때만 의미 — 호스트 id도 0이라 0만으로는 환경음과 구분 못 함)
        public bool HasSource;   // 누가 낸 소리인지 분명함 (2026-09-24, 앙심 귀속)
        public float Time;
    }

    /// <summary>
    /// 전역 소음 버스 (docs/06). 호스트에서만 Emit — 리스너(고양이)가 호스트에만 있으므로.
    /// 소음은 이벤트(즉시 소비), 지속음 없음. 전파 반경 = loudness/100 × 14m,
    /// 벽(NoiseBlocker) 1장당 −40%는 리스너별 GetLoudnessAt에서 계산.
    /// </summary>
    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> OnNoise;

        // 배경 소음 소스별 마스크 (로봇청소기 40·TV 25 — design/cat-ideas/10). 여러 개가 동시에 켜질 수 있어 최댓값을 쓴다.
        private static readonly System.Collections.Generic.Dictionary<object, float> _masks = new();

        /// <summary>호스트: 이 loudness 미만 소리는 배경 소음에 묻힌다. 0이면 없음.</summary>
        public static float MaskLoudness
        {
            get { float m = 0f; foreach (var v in _masks.Values) if (v > m) m = v; return m; }
        }

        /// <summary>호스트: 소스별 배경 소음 켜기(loudness &gt; 0)·끄기(0).</summary>
        public static void SetMask(object source, float loudness)
        {
            if (loudness <= 0f) _masks.Remove(source); else _masks[source] = loudness;
        }

        /// <summary>정적 — 판이 바뀌면 비운다 (고양이 스폰 때).</summary>
        public static void ClearMasks() => _masks.Clear();

        /// <summary>docs/06 기즈모·파문용 최근 이벤트 (호스트 로컬 링버퍼).</summary>
        public static readonly NoiseEvent[] Recent = new NoiseEvent[16];
        private static int _recentIndex;

        private static BalanceConfigSO _balance;
        public static void Configure(BalanceConfigSO balance) => _balance = balance;

        /// <summary>호스트에서만 호출할 것 (정적이라 강제 불가 — 호출부 책임).</summary>
        public static void Emit(Vector3 pos, float loudness, NoiseType type, ulong? sourceClientId = null)
        {
            if (loudness <= 0f) return;
            var e = new NoiseEvent
            {
                Pos = pos,
                Loudness = Mathf.Clamp(loudness, 0f, 100f),
                Type = type,
                Source = sourceClientId ?? 0,
                HasSource = sourceClientId.HasValue,
                Time = UnityEngine.Time.time
            };
            Recent[_recentIndex] = e;
            _recentIndex = (_recentIndex + 1) % Recent.Length;
            OnNoise?.Invoke(e);
        }

        /// <summary>리스너 위치에서의 유효 loudness. 반경 밖 0, 벽 1장 −40% (Linecast 1회 — docs/06 단순 규칙).</summary>
        public static float GetLoudnessAt(in NoiseEvent e, Vector3 listenerPos)
        {
            if (_balance == null) return 0f;
            float radius = e.Loudness / 100f * _balance.MaxNoiseRadius;
            if (Vector3.Distance(e.Pos, listenerPos) > radius) return 0f;

            float loudness = e.Loudness;
            int blockerMask = LayerMask.GetMask("NoiseBlocker");
            if (Physics.Linecast(e.Pos, listenerPos, blockerMask, QueryTriggerInteraction.Ignore))
                loudness *= _balance.WallAttenuation;
            return loudness;
        }

        public static float GetRadius(in NoiseEvent e) =>
            _balance == null ? 0f : e.Loudness / 100f * _balance.MaxNoiseRadius;
    }
}
