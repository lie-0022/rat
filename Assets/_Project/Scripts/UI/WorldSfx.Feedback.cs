using RatGame.Core;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 좋은 순간 소리 (고양이 275) — 도전과제 달성 짜잔, 도감 새 칸 딩, 할당량 채움 빰빠밤, 치즈 먹기 오독오독. 모두 2D, 이벤트·NV 읽기만.
    /// 할당량은 ToastWidget과 같은 방식(적립·할당량 NV를 보고 처음 채운 순간 한 번).
    /// </summary>
    public partial class WorldSfx
    {
        private Run.StageQuota _quotaSeen;
        private bool _quotaWasMet;

        private void PlayReward(AudioClip clip, string what)
        {
            if (_audio == null) return;
            _ui.PlayOneShot(clip, _audio.RewardVolume * Sfx);
            Log.Dev($"좋은 순간 소리: {what}");
        }

        private void OnAchievement(string id, string title) => PlayReward(_audio.AchievementClip, $"도전과제 {id}");
        private void OnCodex(string itemId) => PlayReward(_audio.CodexClip, $"도감 {itemId}");
        private void OnEat(float amount) => PlayReward(_audio.EatClip, "냠냠");

        private void UpdateQuotaJingle()
        {
            var run = Run.RunManager.Instance;
            var quota = run != null && run.IsSpawned ? run.GetComponent<Run.StageQuota>() : null;
            if (quota == null || !quota.IsSpawned || quota.Quota.Value <= 0) { _quotaSeen = null; return; }
            bool met = quota.Met(run.StashedValue.Value);
            if (quota != _quotaSeen) { _quotaSeen = quota; _quotaWasMet = met; return; } // 늦게 들어와 이미 채운 상태면 조용히
            if (met && !_quotaWasMet) PlayReward(_audio.QuotaClip, "할당량 채움");
            _quotaWasMet = met;
        }
    }
}
