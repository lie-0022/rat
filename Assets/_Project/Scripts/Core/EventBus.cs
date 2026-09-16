using System;
using RatGame.Data;
using RatGame.Run;

namespace RatGame.Core
{
    /// <summary>
    /// 로컬 알림 전용 정적 이벤트 버스 (docs/02). 네트워크 동기화는 NGO 담당 — 여기 실으면 안 된다.
    /// UI·사운드·도전과제는 이벤트 구독만으로 동작한다. 구독 해제는 OnDestroy에서 필수.
    /// </summary>
    public static class EventBus
    {
        // Run flow
        public static event Action<int /*zoneIndex*/> ZoneStarted;
        public static event Action<int /*zoneIndex*/, bool /*quotaMet*/> ZoneEnded;
        public static event Action<RunResult> RunEnded;
        public static event Action<int /*current*/, int /*quota*/> QuotaChanged;

        // Player
        public static event Action<ulong /*clientId*/> PlayerDowned;
        public static event Action<ulong /*clientId*/> PlayerRevived;
        /// <summary>핑 수신 (전 클라 — PlayerPing이 서버 중계를 받은 뒤 로컬 발행, 마커 UI용).</summary>
        public static event Action<ulong /*clientId*/, UnityEngine.Vector3 /*worldPos*/> PingReceived;

        // Loot
        public static event Action<LootItemSO, int /*value*/> LootDeposited;
        public static event Action<LootItemSO> LootBroken;

        // Meta
        public static event Action<int> CheeseCoinChanged;
        public static event Action<string /*achievementId*/> AchievementUnlocked;
        /// <summary>내 도감에 새 아이템 등록 (소유 클라 로컬 — 토스트용).</summary>
        public static event Action<string /*itemId*/> CodexUnlocked;

        public static void RaiseZoneStarted(int zoneIndex) => ZoneStarted?.Invoke(zoneIndex);
        public static void RaiseZoneEnded(int zoneIndex, bool quotaMet) => ZoneEnded?.Invoke(zoneIndex, quotaMet);
        public static void RaiseRunEnded(RunResult result) => RunEnded?.Invoke(result);
        public static void RaiseQuotaChanged(int current, int quota) => QuotaChanged?.Invoke(current, quota);
        public static void RaisePlayerDowned(ulong clientId) => PlayerDowned?.Invoke(clientId);
        public static void RaisePlayerRevived(ulong clientId) => PlayerRevived?.Invoke(clientId);
        public static void RaisePingReceived(ulong clientId, UnityEngine.Vector3 worldPos) => PingReceived?.Invoke(clientId, worldPos);
        public static void RaiseLootDeposited(LootItemSO item, int value) => LootDeposited?.Invoke(item, value);
        public static void RaiseLootBroken(LootItemSO item) => LootBroken?.Invoke(item);
        public static void RaiseCheeseCoinChanged(int total) => CheeseCoinChanged?.Invoke(total);
        public static void RaiseAchievementUnlocked(string achievementId) => AchievementUnlocked?.Invoke(achievementId);
        public static void RaiseCodexUnlocked(string itemId) => CodexUnlocked?.Invoke(itemId);
    }
}
