using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 고양이 관계 중재자 (design/cat-ideas/07, 2026-09-24). 호스트 전용 MonoBehaviour — RunManager가 런타임에 붙인다.
    /// 고양이가 2마리 이상일 때만 일한다. 지금 관계는 **앙숙** 하나: 싸울 수 있는 두 고양이가 4m 안에서 서로 보이면
    /// 70% 싸움(15s, 뒤 30s 재발 금지) / 30% 서로 무시(10s 재판정 금지). 각 CatBrain은 상대를 몰라도 된다 — 중재자가 지시한다.
    /// 엄마·아기·짝꿍(부르기·협공·공동 수면)은 다음 단계.
    /// </summary>
    public class CatRelation : MonoBehaviour
    {
        private BalanceConfigSO _balance;
        private float _nextCheck;
        private float _nextScan;
        private CatBrain[] _cats = new CatBrain[0];
        private readonly Dictionary<(int, int), float> _pairCooldown = new();

        public int FightsStarted { get; private set; }

        public void Init(BalanceConfigSO balance) => _balance = balance;

        private void Update()
        {
            if (_balance == null || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.25f;
            if (Time.time >= _nextScan) { _nextScan = Time.time + 2f; _cats = FindObjectsByType<CatBrain>(FindObjectsSortMode.None); }
            if (_cats.Length < 2) return;

            for (int i = 0; i < _cats.Length; i++)
                for (int j = i + 1; j < _cats.Length; j++)
                    CheckPair(_cats[i], _cats[j]);
        }

        private void CheckPair(CatBrain a, CatBrain b)
        {
            if (a == null || b == null || !a.IsSpawned || !b.IsSpawned) return;
            if (!a.CanFight || !b.CanFight) return;
            var key = a.GetInstanceID() < b.GetInstanceID() ? (a.GetInstanceID(), b.GetInstanceID()) : (b.GetInstanceID(), a.GetInstanceID());
            if (_pairCooldown.TryGetValue(key, out float until) && Time.time < until) return;

            Vector3 pa = a.transform.position + Vector3.up * 0.5f, pb = b.transform.position + Vector3.up * 0.5f;
            if (Vector3.Distance(pa, pb) > _balance.CatFightDistance) return;
            int blockMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker");
            if (Physics.Linecast(pa, pb, blockMask, QueryTriggerInteraction.Ignore)) return; // 벽 너머면 모른다

            if (Random.value >= _balance.CatFightChance)
            {
                _pairCooldown[key] = Time.time + _balance.CatFightDeclineCooldown;
                Log.Dev($"고양이 관계: {a.name}·{b.name} 서로 째려보고 지나감");
                return;
            }
            _pairCooldown[key] = Time.time + _balance.CatFightSeconds + _balance.CatFightCooldown;
            a.ServerStartFight(b, _balance.CatFightSeconds);
            b.ServerStartFight(a, _balance.CatFightSeconds);
            FightsStarted++;
            Log.Dev($"고양이 관계: 앙숙 {a.name}·{b.name} 싸움 시작 ({_balance.CatFightSeconds}s)");
        }
    }
}
