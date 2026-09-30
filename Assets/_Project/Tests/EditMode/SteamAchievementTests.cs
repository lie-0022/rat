using System.Collections.Generic;
using NUnit.Framework;
using RatGame.Core;
using RatGame.Net;

namespace RatGame.Tests
{
    /// <summary>
    /// Steam 도전과제 연동 (고양이 326) — 실제 Steam 없이 가짜 받는 곳으로. 진짜 켜짐은 데모 AppID가 생긴 뒤 사람이 확인(U-326).
    /// </summary>
    public class SteamAchievementTests
    {
        private sealed class FakeSink : IAchievementSink
        {
            public readonly List<string> Sets = new();
            public int Stores;
            public bool Set(string apiName) { Sets.Add(apiName); return true; }
            public void Store() => Stores++;
        }

        [Test]
        public void 시작할_때_이미_깬_것을_한_번에_켜고_한_번_저장()
        {
            var sink = new FakeSink();
            var bridge = new SteamAchievementBridge(sink);
            int n = bridge.SyncAll(new[] { "ach_ghost", "", "ach_deep_rat" });
            Assert.AreEqual(2, n);
            CollectionAssert.AreEqual(new[] { "ach_ghost", "ach_deep_rat" }, sink.Sets);
            Assert.AreEqual(1, sink.Stores);
        }

        [Test]
        public void 깬_게_없으면_저장도_안_함()
        {
            var sink = new FakeSink();
            Assert.AreEqual(0, new SteamAchievementBridge(sink).SyncAll(new string[0]));
            Assert.AreEqual(0, sink.Stores);
        }

        [Test]
        public void 새로_깨면_바로_켜고_버리면_더는_안_받음()
        {
            var sink = new FakeSink();
            var bridge = new SteamAchievementBridge(sink);
            bridge.Attach();
            bridge.Attach(); // 두 번 붙여도 한 번만
            EventBus.RaiseAchievementUnlocked("ach_medic", "구조대");
            CollectionAssert.AreEqual(new[] { "ach_medic" }, sink.Sets);
            Assert.AreEqual(1, sink.Stores);

            bridge.Dispose();
            EventBus.RaiseAchievementUnlocked("ach_survivor", "생존자");
            Assert.AreEqual(1, sink.Sets.Count);
        }
    }
}
