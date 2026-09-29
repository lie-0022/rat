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

        /// <summary>찍찍 — 높은 음이 두 번 짧게 (쥐 목소리, 고양이 244).</summary>
        public static AudioClip Squeak(float freq, float seconds) => Get($"squeak{freq}-{seconds}", seconds, (t, u) =>
        {
            float half = u < 0.5f ? u / 0.5f : (u - 0.5f) / 0.5f;           // 두 토막
            float f = freq * (1f + 0.25f * Mathf.Sin(half * Mathf.PI));      // 토막마다 올랐다 내려옴
            _phase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Sin(Mathf.Clamp01(half / 0.8f) * Mathf.PI) * (half < 0.8f ? 1f : 0f);
            return env * Mathf.Sin(_phase);
        });

        /// <summary>쨍 — 딱딱한 것이 부딪히는 소리. 어긋난 배음 셋이 빨리 사라지고, 처음에 잡음 한 번 (고양이 244).</summary>
        public static AudioClip Clink(float freq, float seconds) => Get($"clink{freq}-{seconds}", seconds, (t, u) =>
        {
            float decay = Mathf.Exp(-6f * u);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t);
            float hit = t < 0.015f ? Noise() * (1f - t / 0.015f) : 0f;
            return decay * tone * 0.5f + hit;
        });

        /// <summary>딸랑 — 두 음이 차례로 (정산, 고양이 244).</summary>
        public static AudioClip Chime(float f0, float f1, float seconds) => Get($"chime{f0}-{f1}-{seconds}", seconds, (t, u) =>
        {
            float gap = seconds * 0.35f;
            float a = Mathf.Exp(-5f * t) * Mathf.Sin(2f * Mathf.PI * f0 * t);
            float b = t < gap ? 0f : Mathf.Exp(-4f * (t - gap)) * Mathf.Sin(2f * Mathf.PI * f1 * (t - gap));
            return 0.5f * a + 0.6f * b;
        });

        /// <summary>쏴아 — 물이 쏟아지는 잡음(반복용). grit이 클수록 거칠다 (고양이 245).</summary>
        public static AudioClip Rush(float grit, float seconds) => Get($"rush{grit}-{seconds}", seconds, (t, u) =>
        {
            _low += (Noise() - _low) * grit;
            return _low * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 3f * u)); // 한 바퀴에 세 번 출렁 — 이음매 없음
        });

        /// <summary>웅얼웅얼 — 멀리서 들리는 말소리(TV). 잡음을 음절 박자로 켰다 껐다 (반복용, 고양이 245).</summary>
        public static AudioClip Babble(float syllables, float seconds) => Get($"babble{syllables}-{seconds}", seconds, (t, u) =>
        {
            _low += (Noise() - _low) * 0.25f;
            float syl = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * syllables * t)) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.7f * t));
            float voice = Mathf.Sin(2f * Mathf.PI * (180f + 40f * Mathf.Sin(2f * Mathf.PI * 1.3f * t)) * t);
            return syl * (0.6f * _low + 0.4f * voice);
        });

        /// <summary>톡 — 작은 발이 바닥을 딛는 소리. 둔한 잡음 + 낮은 울림이 금방 사라진다 (고양이 247).</summary>
        public static AudioClip Tap(float body, float seconds) => Get($"tap{body}-{seconds}", seconds, (t, u) =>
        {
            _low += (Noise() - _low) * 0.3f;
            float decay = Mathf.Exp(-9f * u);
            return decay * (0.7f * _low + 0.5f * Mathf.Sin(2f * Mathf.PI * body * t));
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
