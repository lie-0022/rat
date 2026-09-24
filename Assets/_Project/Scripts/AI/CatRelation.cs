using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>두 마리 관계 (design/cat-ideas/07). 2마리가 처음 보일 때 뽑는다.</summary>
    public enum CatRelationKind { Rivals, Buddies, MotherKitten }

    /// <summary>
    /// 고양이 관계 중재자 (design/cat-ideas/07, 2026-09-24). 호스트 전용 MonoBehaviour — RunManager가 런타임에 붙인다.
    /// 고양이가 2마리 이상일 때만 일한다. 관계는 앙숙 또는 짝꿍(catBuddyChance로 뽑음).
    ///  앙숙: 싸울 수 있는 두 고양이가 4m 안에서 서로 보이면 70% 싸움(15s, 뒤 30s 재발 금지) / 30% 서로 무시(10s 재판정 금지).
    ///  짝꿍: 한 마리가 쫓으면 다른 마리가 협공, 한 마리가 자면 다른 마리도 와서 같이 잔다.
    /// 각 CatBrain은 상대를 몰라도 된다 — 중재자가 지시한다. 엄마·아기는 다음 단계.
    /// </summary>
    public class CatRelation : MonoBehaviour
    {
        /// <summary>2마리가 처음 보일 때 뽑는다 (앙숙/짝꿍). ServerSetKind로 지정 가능.</summary>
        public CatRelationKind Kind { get; private set; }
        private bool _kindChosen;
        private float _nextKittenCall;

        public void ServerSetKind(CatRelationKind kind) { Kind = kind; _kindChosen = true; Log.Dev($"고양이 관계: {kind}"); }

        private void OnEnable() => CatBrain.ServerStateChanged += OnCatState;
        private void OnDisable() => CatBrain.ServerStateChanged -= OnCatState;

        // 짝꿍: 한 마리가 쫓으면 다른 마리가 협공, 한 마리가 자면 다른 마리도 와서 같이 잔다
        private void OnCatState(CatBrain cat, CatState prev, CatState next)
        {
            if (!_kindChosen || _balance == null) return;
            if (Kind == CatRelationKind.MotherKitten)
            {
                // 아기가 놀라면 냐앙 → 엄마가 달려온다. 자면 같이 잔다
                if (cat.IsKitten && (next == CatState.Suspicious || next == CatState.Chase) && Time.time >= _nextKittenCall)
                {
                    _nextKittenCall = Time.time + _balance.CatKittenCallCooldown;
                    cat.ServerKittenCallCue();
                    foreach (var other in _cats) if (other != null && other != cat && other.IsSpawned && !other.IsKitten) other.ServerRespond(cat.transform.position);
                }
                else if (next == CatState.Sleep && prev != CatState.Sleep)
                    foreach (var other in _cats) if (other != null && other != cat && other.IsSpawned) other.ServerJoinSleep();
                return;
            }
            if (Kind != CatRelationKind.Buddies) return;
            foreach (var other in _cats)
            {
                if (other == null || other == cat || !other.IsSpawned) continue;
                if (next == CatState.Chase && cat.ChaseTarget != null && other.CanAssistBuddy
                    && Vector3.Distance(other.transform.position, cat.transform.position) <= _balance.CatFlankRange)
                    other.ServerFlank(cat.ChaseTarget);
                else if (next == CatState.Sleep && prev != CatState.Sleep)
                    other.ServerJoinSleep();
            }
        }

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
            // 아기가 있으면 추첨과 상관없이 엄마·아기 — 아기 성격은 스폰 직후에 지정되므로 매 스캔 확인한다
            bool kitten = false; foreach (var c in _cats) if (c != null && c.IsSpawned && c.IsKitten) kitten = true;
            if (kitten && Kind != CatRelationKind.MotherKitten) ServerSetKind(CatRelationKind.MotherKitten);
            else if (!_kindChosen)
                ServerSetKind(Random.value < _balance.CatBuddyChance ? CatRelationKind.Buddies : CatRelationKind.Rivals);
            if (Kind != CatRelationKind.Rivals) return; // 싸움은 앙숙만

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
