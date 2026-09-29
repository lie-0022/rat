using System.Collections.Generic;
using UnityEngine;

namespace RatGame.Core
{
    /// <summary>
    /// 그레이박스 소리 합성 (고양이 242, plan cat-242) — 소리 파일이 없는 동안 코드로 만든 짧은 음.
    /// 같은 요청은 한 번만 만들어 캐시한다. 진짜 소리가 들어오면 부르는 쪽에서 클립만 바꿔 끼운다.
    /// </summary>
    public static class ToneSynth
    {
        private const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> Cache = new();

        /// <summary>높이가 f0 → f1로 미끄러지는 짧은 음 (고양이 "냐?" — 끝이 올라감). 배음 하나를 섞어 삑 소리보다 목소리에 가깝게.</summary>
        public static AudioClip Chirp(float f0, float f1, float seconds) => Get($"chirp{f0}-{f1}-{seconds}", seconds, (t, u) =>
        {
            float f = Mathf.Lerp(f0, f1, u * u);
            _phase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Min(1f, t / 0.02f) * (1f - u) * (0.6f + 0.4f * Mathf.Sin(u * Mathf.PI));
            return env * (0.7f * Mathf.Sin(_phase) + 0.3f * Mathf.Sin(2f * _phase));
        });

        /// <summary>낮은 그르렁 — 톱니파를 빠르게 떨리게(초당 tremolo번). 처음과 끝이 이어지게 만들어 반복 재생용.</summary>
        public static AudioClip Growl(float freq, float tremolo, float seconds) => Get($"growl{freq}-{tremolo}-{seconds}", seconds, (t, u) =>
        {
            float saw = 2f * (t * freq - Mathf.Floor(t * freq + 0.5f));
            float trem = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * tremolo * t);
            return 0.6f * saw * trem + 0.15f * Noise();
        });

        /// <summary>짧은 쉭 소리 (앞발 스윙) — 잡음이 빨리 사라진다.</summary>
        public static AudioClip Swipe(float seconds) => Get($"swipe{seconds}", seconds, (t, u) =>
        {
            _low += (Noise() - _low) * Mathf.Lerp(0.6f, 0.08f, u); // 뒤로 갈수록 둔해짐
            return _low * Mathf.Min(1f, t / 0.01f) * (1f - u) * (1f - u) * 1.6f;
        });

        /// <summary>코골이 — 들숨(거친 잡음 + 낮은 떨림)과 날숨(조용)이 한 번. 반복 재생용.</summary>
        public static AudioClip Snore(float seconds) => Get($"snore{seconds}", seconds, (t, u) =>
        {
            float breath = u < 0.45f ? Mathf.Sin(u / 0.45f * Mathf.PI) : 0.25f * Mathf.Sin((u - 0.45f) / 0.55f * Mathf.PI);
            _low += (Noise() - _low) * 0.12f;
            float rattle = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 32f * t); // 목젖 떨림
            return breath * (0.9f * _low * rattle + 0.25f * Mathf.Sin(2f * Mathf.PI * 70f * t));
        });

        private static float _phase, _low;
        private static uint _seed = 1;
        private static float Noise() { _seed = _seed * 1664525u + 1013904223u; return (_seed >> 8) / 8388608f - 1f; }

        private static AudioClip Get(string key, float seconds, System.Func<float, float, float> sample)
        {
            if (Cache.TryGetValue(key, out var clip) && clip != null) return clip;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            _phase = 0f; _low = 0f; _seed = 1;
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) { data[i] = sample(i / (float)Rate, i / (float)n); peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.9f; // 소리마다 크기를 맞추고 음량은 SO에서
            clip = AudioClip.Create("synth_" + key, n, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }
    }
}
