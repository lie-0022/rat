using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Meta
{
    /// <summary>
    /// 도전과제 (docs/11 AchievementService, 고양이 219). 개인 기록 — 각자 기기에서 `EventBus.AchievementStat`을 받아 세이브의 Stats에 쌓고,
    /// 목표에 닿으면 CompletedAchievementIds에 넣고 `EventBus.AchievementUnlocked`(알림은 UI가 구독 — 규칙 3).
    /// 싱글톤 금지(docs/02)라 정적 Instance 없이 스스로 생기는 DontDestroyOnLoad 컴포넌트 — 다른 코드는 이벤트로만 부른다.
    /// 보상 스킨은 아직 잠그지 않는다 (판단 부탁 14).
    /// </summary>
    public class AchievementService : MonoBehaviour
    {
        private AchievementSO[] _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("AchievementService");
            DontDestroyOnLoad(go);
            go.AddComponent<AchievementService>();
        }

        private void Awake() => _all = Resources.LoadAll<AchievementSO>("Achievements");
        private void OnEnable() => EventBus.AchievementStat += OnStat;
        private void OnDisable() => EventBus.AchievementStat -= OnStat;

        private void OnStat(string key, int value, bool keepMax)
        {
            var data = SaveService.Data;
            var entry = data.Stats.Find(e => e.Key == key);
            if (entry == null) { entry = new StatEntry { Key = key }; data.Stats.Add(entry); }
            int before = entry.Value;
            entry.Value = keepMax ? Mathf.Max(entry.Value, value) : entry.Value + value;
            if (entry.Value == before) return;
            Log.Dev($"도전과제 통계: {key} {before} → {entry.Value}");

            foreach (var a in _all)
            {
                if (a.StatKey != key || entry.Value < a.Target || data.CompletedAchievementIds.Contains(a.Id)) continue;
                data.CompletedAchievementIds.Add(a.Id);
                Log.Dev($"도전과제 달성: {a.Id} ({a.Title})");
                EventBus.RaiseAchievementUnlocked(a.Id, a.Title);
            }
            SaveService.Save();
        }
    }
}
