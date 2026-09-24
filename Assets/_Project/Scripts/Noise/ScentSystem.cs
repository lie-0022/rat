using RatGame.Data;
using UnityEngine;

namespace RatGame.Noise
{
    /// <summary>냄새 자국 하나. 강도는 시간에 따라 선형 감소 — 저장은 처음 강도·시각만.</summary>
    public struct ScentMark
    {
        public Vector3 Pos;
        public float InitialStrength;
        public float Time;
        public ulong Source;   // 남긴 쥐
        public int Seq;        // 전역 증가 번호 — 같은 쥐 자국의 순서
        public bool Valid;
    }

    /// <summary>
    /// 냄새 자국 버퍼 (design/cat-ideas/06, 2026-09-24). NoiseSystem과 같은 모양의 정적 호스트 전용 저장소 — 규칙 1.
    /// 링 버퍼(기본 32)라 오래된 자국부터 밀려난다. 강도 = 처음 강도 − 감쇠×경과, 0 이하면 없는 것.
    /// </summary>
    public static class ScentSystem
    {
        private static ScentMark[] _marks = new ScentMark[32];
        private static int _next;
        private static int _seq;
        private static BalanceConfigSO _balance;

        public static void Configure(BalanceConfigSO balance)
        {
            _balance = balance;
            int cap = Mathf.Max(8, balance.ScentMaxMarks);
            if (_marks.Length != cap) { _marks = new ScentMark[cap]; _next = 0; }
        }

        /// <summary>씬 전환·세션 시작 때 비우기 (정적이라 플레이 모드를 넘어 남는다).</summary>
        public static void Clear()
        {
            for (int i = 0; i < _marks.Length; i++) _marks[i].Valid = false;
            _next = 0;
        }

        public static int Count
        {
            get { int n = 0; for (int i = 0; i < _marks.Length; i++) if (StrengthOf(_marks[i]) > 0f) n++; return n; }
        }

        public static ScentMark Add(Vector3 pos, float strength, ulong source)
        {
            var m = new ScentMark { Pos = pos, InitialStrength = strength, Time = UnityEngine.Time.time, Source = source, Seq = ++_seq, Valid = true };
            _marks[_next] = m;
            _next = (_next + 1) % _marks.Length;
            return m;
        }

        public static float StrengthOf(in ScentMark m)
        {
            if (!m.Valid || _balance == null) return 0f;
            return m.InitialStrength - _balance.ScentDecayPerSec * (UnityEngine.Time.time - m.Time);
        }

        /// <summary>pos 반경 안 강도 ≥ minStrength인 가장 가까운 자국. filter(source, seq)가 false면 건너뜀.</summary>
        public static bool FindNearest(Vector3 pos, float radius, float minStrength, System.Func<ulong, int, bool> filter, out ScentMark found)
        {
            found = default;
            float best = radius * radius;
            bool ok = false;
            for (int i = 0; i < _marks.Length; i++)
            {
                var m = _marks[i];
                if (StrengthOf(m) < minStrength) continue;
                float d = (m.Pos - pos).sqrMagnitude;
                if (d > best) continue;
                if (filter != null && !filter(m.Source, m.Seq)) continue;
                best = d; found = m; ok = true;
            }
            return ok;
        }

        /// <summary>같은 쥐가 afterSeq 다음에 남긴 자국(가장 이른 것).</summary>
        public static bool NextInTrail(ulong source, int afterSeq, float minStrength, out ScentMark found)
        {
            found = default;
            int bestSeq = int.MaxValue;
            bool ok = false;
            for (int i = 0; i < _marks.Length; i++)
            {
                var m = _marks[i];
                if (m.Source != source || m.Seq <= afterSeq || m.Seq >= bestSeq) continue;
                if (StrengthOf(m) < minStrength) continue;
                bestSeq = m.Seq; found = m; ok = true;
            }
            return ok;
        }
    }
}
