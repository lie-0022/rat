using System;
using RatGame.Data;
using RatGame.Run;

namespace RatGame.Core
{
    /// <summary>집주인 이벤트 종류 (design/cat-ideas/10). append-only — RPC로 byte 전송.</summary>
    public enum HouseEventKind : byte { CallAway, Feeding, Doorbell /* 쥐가 누른 초인종 — 부르기와 같은 부재 */, Vacuum /* 로봇청소기 */, TV /* TV 켜짐 */, LightOn /* 불 켜짐 */, Window /* 창문 바람 */, NewTraps /* 집주인이 덫을 놓음 — 벽 속 (고양이 89) */, Flush /* 배관에 물 — 벽 속 (고양이 97) */ }
    /// <summary>기지 오브젝트가 여는 패널 종류 (규칙 3 — World는 UI 타입을 모른다).</summary>
    public enum WorldPanelKind : byte { Shop, Mirror, Codex, StageShop /* 새 루프 목적지 상점 (고양이 65) */ }
    public enum HouseEventPhase : byte { Warn, Start, End }
    /// <summary>고양이 루틴 예고 소리 (design/cat-ideas/02). append-only — RPC로 byte 전송.</summary>
    public enum CatCueKind : byte { Food, Litter, Sun, Bed, Water, Ambush, KittenCall, GuardNap /* 문지기가 졺 (고양이 101) */, PatrolNap /* 순찰꾼이 졺 */, Snore /* 깊은 잠 코골이 (고양이 152) */, SnoreStop /* 코골이 멈춤 = 곧 깸 */ }

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
        /// <summary>벽 속 스테이지 안내 (각 클라 로컬 — GridZoneBuilder.StageBriefingClientRpc가 발행, 고양이 81). flags: 1 아기, 2 문지기, 4 배관 고리.</summary>
        public static event Action<int /*stage*/, int /*stages*/, int /*quota*/, byte /*flags*/, byte /*modifier*/> StageBriefing;

        // Player
        public static event Action<ulong /*clientId*/> PlayerDowned;
        public static event Action<ulong /*clientId*/> PlayerRevived;
        /// <summary>핑 수신 (전 클라 — PlayerPing이 서버 중계를 받은 뒤 로컬 발행, 마커 UI용).</summary>
        public static event Action<ulong /*clientId*/, UnityEngine.Vector3 /*worldPos*/> PingReceived;
        /// <summary>킁킁 (소유 클라 로컬 — 목적지 쪽 다음 문까지 냄새 줄기, 고양이 76).</summary>
        public static event Action<UnityEngine.Vector3 /*from*/, UnityEngine.Vector3 /*to*/, float /*seconds*/> SniffHint;
        /// <summary>킁킁 — 가까운 음식 냄새 김 (소유 클라 로컬, 고양이 150).</summary>
        public static event Action<System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3>, float /*seconds*/> SniffFood;

        // Loot
        public static event Action<LootItemSO, int /*value*/> LootDeposited;
        public static event Action<LootItemSO> LootBroken;

        // Meta
        public static event Action<int> CheeseCoinChanged;
        public static event Action<string /*achievementId*/> AchievementUnlocked;
        /// <summary>내 도감에 새 아이템 등록 (소유 클라 로컬 — 토스트용).</summary>
        public static event Action<string /*itemId*/> CodexUnlocked;
        /// <summary>집주인 이벤트 알림 (각 클라 로컬 — RunManager.HouseEventClientRpc가 발행, 2026-09-24).</summary>
        public static event Action<HouseEventKind, HouseEventPhase> HouseEvent;
        /// <summary>고양이가 루틴 스팟으로 출발 (각 클라 로컬 — CatBrain.CatCueClientRpc가 발행).</summary>
        public static event Action<CatCueKind, UnityEngine.Vector3, float> CatCue; // 마지막 = 들리는 거리 배율(큰 고양이 몸, 고양이 149)
        /// <summary>큰 소음 파문 (docs/06 클라 시각화) — 모든 클라.</summary>
        public static event Action<UnityEngine.Vector3, float /*loudness*/> NoiseRipple;
        /// <summary>쥐가 찍찍 (전 클라 — PlayerNoiseEmitter.SqueakClientRpc, 고양이 158).</summary>
        public static event Action<ulong, UnityEngine.Vector3> RatSqueak;
        /// <summary>기지 오브젝트가 "이 클라에 패널을 열어 달라" (자판기·거울·도감 — 규칙 3, 2026-09-24 고양이 52). source = 그 오브젝트.</summary>
        public static event Action<WorldPanelKind, UnityEngine.Object> WorldPanelRequested;
        /// <summary>패널을 연 오브젝트가 사라졌다(씬 전환) — 그 패널을 닫는다.</summary>
        public static event Action<UnityEngine.Object> WorldPanelSourceGone;
        /// <summary>상점 구매 결과 (구매한 클라).</summary>
        public static event Action<bool, string> ShopPurchaseResult;
        /// <summary>내가 치즈를 먹었다 (소유 클라, 회복량).</summary>
        public static event Action<float> CheeseEaten;
        /// <summary>내가 쥐덫 미끼를 집음 — 웅크려 성공(true) / 서서 탁(false) (고양이 129).</summary>
        public static event Action<bool> TrapBait;
        /// <summary>미끼 얹힌 쥐덫에 처음 다가감 — 규칙 안내 (고양이 132).</summary>
        public static event Action TrapBaitNear;
        /// <summary>고양이에게 방울이 달림 (모든 클라, 고양이 140).</summary>
        public static event Action<UnityEngine.Vector3> CatBelled;

        public static void RaiseZoneStarted(int zoneIndex) => ZoneStarted?.Invoke(zoneIndex);
        public static void RaiseZoneEnded(int zoneIndex, bool quotaMet) => ZoneEnded?.Invoke(zoneIndex, quotaMet);
        public static void RaiseRunEnded(RunResult result) => RunEnded?.Invoke(result);
        public static void RaiseQuotaChanged(int current, int quota) => QuotaChanged?.Invoke(current, quota);
        public static void RaiseStageBriefing(int stage, int stages, int quota, byte flags, byte modifier = 0) => StageBriefing?.Invoke(stage, stages, quota, flags, modifier);
        public static void RaisePlayerDowned(ulong clientId) => PlayerDowned?.Invoke(clientId);
        public static void RaisePlayerRevived(ulong clientId) => PlayerRevived?.Invoke(clientId);
        public static void RaisePingReceived(ulong clientId, UnityEngine.Vector3 worldPos) => PingReceived?.Invoke(clientId, worldPos);
        public static void RaiseSniffHint(UnityEngine.Vector3 from, UnityEngine.Vector3 to, float seconds) => SniffHint?.Invoke(from, to, seconds);
        public static void RaiseSniffFood(System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3> spots, float seconds) => SniffFood?.Invoke(spots, seconds);
        public static void RaiseLootDeposited(LootItemSO item, int value) => LootDeposited?.Invoke(item, value);
        public static void RaiseLootBroken(LootItemSO item) => LootBroken?.Invoke(item);
        public static void RaiseCheeseCoinChanged(int total) => CheeseCoinChanged?.Invoke(total);
        public static void RaiseAchievementUnlocked(string achievementId) => AchievementUnlocked?.Invoke(achievementId);
        public static void RaiseCodexUnlocked(string itemId) => CodexUnlocked?.Invoke(itemId);
        public static void RaiseHouseEvent(HouseEventKind kind, HouseEventPhase phase) => HouseEvent?.Invoke(kind, phase);
        public static void RaiseNoiseRipple(UnityEngine.Vector3 pos, float loudness) => NoiseRipple?.Invoke(pos, loudness);
        public static void RaiseRatSqueak(ulong owner, UnityEngine.Vector3 pos) => RatSqueak?.Invoke(owner, pos);
        public static void RaiseCheeseEaten(float amount) => CheeseEaten?.Invoke(amount);
        public static void RaiseTrapBait(bool sneaky) => TrapBait?.Invoke(sneaky);
        public static void RaiseTrapBaitNear() => TrapBaitNear?.Invoke();
        public static void RaiseCatBelled(UnityEngine.Vector3 at) => CatBelled?.Invoke(at);
        public static void RaiseWorldPanelRequested(WorldPanelKind kind, UnityEngine.Object source) => WorldPanelRequested?.Invoke(kind, source);
        public static void RaiseWorldPanelSourceGone(UnityEngine.Object source) => WorldPanelSourceGone?.Invoke(source);
        public static void RaiseShopPurchaseResult(bool ok, string message) => ShopPurchaseResult?.Invoke(ok, message);
        public static void RaiseCatCue(CatCueKind kind, UnityEngine.Vector3 catPos, float hearScale = 1f) => CatCue?.Invoke(kind, catPos, hearScale);
    }
}
