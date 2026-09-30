using NUnit.Framework;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 순수 로직 빠른 시험 (고양이 289) — 플레이 없이 몇 초. 파일을 쓰는 것(세이브·설정)은 사용자 파일을 건드리니 넣지 않는다.
    /// 메뉴 Window → General → Test Runner → EditMode.
    /// </summary>
    public class LogicTests
    {
        private static BalanceConfigSO Balance => AssetDatabase.LoadAssetAtPath<BalanceConfigSO>("Assets/_Project/Data/Balance/BalanceConfig.asset");

        [Test]
        public void 번역_틀과_묶음_왕복()
        {
            string packed = Loc.Pack("누계 {0} · {1}", 120, "치즈");
            Assert.AreEqual(Loc.F("누계 {0} · {1}", 120, Loc.T("치즈")), Loc.Unpack(packed));
            Assert.AreEqual("그냥 문장", Loc.Unpack("그냥 문장"));
        }

        [Test]
        public void 번역_틀이_잘못돼도_예외_없이()
        {
            Assert.DoesNotThrow(() => Loc.F("값 {0}", 1));
            Assert.AreEqual(null, Loc.Unpack(null));
        }

        [Test]
        public void 할당량은_스테이지마다_늘어남()
        {
            var b = Balance;
            Assert.NotNull(b);
            for (int s = 1; s < 5; s++) Assert.Greater(b.StageQuota(s + 1), b.StageQuota(s));
            Assert.AreEqual(b.StageQuota(1), b.StageQuota(0)); // 0 이하도 1과 같게
        }

        [Test]
        public void 대형_자리는_특대면_더_많음()
        {
            var b = Balance;
            Assert.GreaterOrEqual(b.HeavyCarrySlots(100f), b.HeavyCarrySlots(5f));
            Assert.AreEqual(4, b.HeavyCarrySlots(20f)); // docs/05 표 — 특대 4자리
        }

        [Test]
        public void 하중_속도_배수는_범위_안()
        {
            var b = Balance;
            foreach (float load in new[] { 0f, 1f, 5f, 20f, 100f })
            {
                float m = b.GetCarrySpeedMultiplier(load);
                Assert.That(m, Is.InRange(0f, 1f));
            }
            Assert.GreaterOrEqual(b.GetCarrySpeedMultiplier(1f), b.GetCarrySpeedMultiplier(20f));
        }

        [Test]
        public void 관심_점수는_유인_종류_순서대로_커짐()
        {
            var b = Balance;
            float prev = -1f;
            foreach (AttentionKind k in System.Enum.GetValues(typeof(AttentionKind)))
            {
                float s = b.AttentionScore((int)k);
                Assert.Greater(s, prev, $"{k} 점수가 앞 종류보다 커야 함 (design/cat-ideas/13 표)");
                prev = s;
            }
            Assert.That(b.BoredomMultiplier, Is.InRange(0f, 1f));
        }

        [Test]
        public void 합성음은_길이가_맞고_캐시됨()
        {
            var a = ToneSynth.Chirp(500f, 800f, 0.3f);
            Assert.AreEqual(0.3f, a.length, 0.01f);
            Assert.AreSame(a, ToneSynth.Chirp(500f, 800f, 0.3f));
            Assert.AreEqual(1f, ToneSynth.Growl(58f, 22f, 1f).length, 0.01f);
        }

        [Test]
        public void 소리_설정의_클립은_모두_만들어짐()
        {
            var a = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            Assert.NotNull(a);
            foreach (var p in typeof(GrayboxAudioSO).GetProperties())
                if (p.PropertyType == typeof(AudioClip)) Assert.NotNull(p.GetValue(a), p.Name);
            foreach (CatBlunderKind k in System.Enum.GetValues(typeof(CatBlunderKind)))
                if (k != CatBlunderKind.None) Assert.NotNull(a.BlunderClip(k), k.ToString());
            foreach (HouseEventKind k in new[] { HouseEventKind.TV, HouseEventKind.Vacuum, HouseEventKind.Flush })
                Assert.NotNull(a.HouseLoopClip(k), k.ToString());
        }

        [Test]
        public void 처음_언어는_한국어_시스템만_한국어() // 스팀 데모에서 외국 사용자가 한국어로 시작하지 않게 (고양이 323)
        {
            Assert.AreEqual("ko", RatGame.Core.SettingsService.DefaultLanguageFor(SystemLanguage.Korean));
            Assert.AreEqual("en", RatGame.Core.SettingsService.DefaultLanguageFor(SystemLanguage.English));
            Assert.AreEqual("en", RatGame.Core.SettingsService.DefaultLanguageFor(SystemLanguage.Japanese));
            Assert.AreEqual("en", RatGame.Core.SettingsService.DefaultLanguageFor(SystemLanguage.Unknown));
        }

        [Test]
        public void 플레이_기록_한_줄_형식() // 엑셀로 여는 CSV — 칸 순서·소수점(문화권 무관)·결과 낱말 (고양이 330)
        {
            var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE"); // 쉼표 소수점 문화권에서도
            try
            {
                string row = RatGame.Meta.PlayLog.Row(new System.DateTime(2026, 10, 1, 9, 5, 3), 3, 300, 470, 40, "클리어", 187.6f, 2, 1, "Blackout", 12345);
                Assert.AreEqual("2026-10-01 09:05:03,3,300,470,40,클리어,188,2,1,Blackout,12345", row);
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = saved; }
        }
    }
}
