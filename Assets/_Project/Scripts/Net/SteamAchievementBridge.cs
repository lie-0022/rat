using System;
using System.Collections.Generic;
using RatGame.Core;

namespace RatGame.Net
{
    /// <summary>도전과제를 받아 적는 곳 — 실제는 Steam, 시험은 가짜.</summary>
    public interface IAchievementSink
    {
        /// <summary>그 API 이름의 도전과제를 켠다 (저장은 Store에서 한 번에).</summary>
        bool Set(string apiName);
        void Store();
    }

    /// <summary>
    /// 로컬 도전과제(AchievementService, 세이브의 CompletedAchievementIds)를 Steam 도전과제로 옮긴다 (docs/11 "스팀 연동 P1", 고양이 326).
    /// Steam API 이름 = 로컬 아이디(ach_…) — Steamworks에 같은 이름으로 등록해야 켜진다(steam/README.md).
    /// 시작할 때 이미 깬 것을 한 번에 맞추고(오프라인에서 깬 것), 새로 깨면 바로 켠다. 이벤트만 구독(규칙 3), NetworkLauncher가 Steam과 같이 만들고 버린다.
    /// </summary>
    public sealed class SteamAchievementBridge : IDisposable
    {
        private readonly IAchievementSink _sink;
        private bool _attached;

        public SteamAchievementBridge(IAchievementSink sink) => _sink = sink;

        /// <summary>이미 깬 것을 모두 켜고 한 번 저장. 켠 개수를 돌려준다.</summary>
        public int SyncAll(IEnumerable<string> completedIds)
        {
            int set = 0;
            foreach (var id in completedIds)
                if (!string.IsNullOrEmpty(id) && _sink.Set(id)) set++;
            if (set > 0) _sink.Store();
            return set;
        }

        public void Attach()
        {
            if (_attached) return;
            EventBus.AchievementUnlocked += OnUnlocked;
            _attached = true;
        }

        public void Dispose()
        {
            if (!_attached) return;
            EventBus.AchievementUnlocked -= OnUnlocked;
            _attached = false;
        }

        private void OnUnlocked(string id, string title)
        {
            bool ok = _sink.Set(id);
            _sink.Store();
            Log.Dev($"Steam 도전과제: {id} {(ok ? "켬" : "실패 — Steamworks에 같은 API 이름이 없음?")}");
        }
    }

    /// <summary>Facepunch Steamworks로 적는 실제 받는 곳.</summary>
    public sealed class SteamAchievementSink : IAchievementSink
    {
        public bool Set(string apiName) => new Steamworks.Data.Achievement(apiName).Trigger(apply: false); // 저장은 Store에서 한 번에
        public void Store() => Steamworks.SteamUserStats.StoreStats();
    }
}
