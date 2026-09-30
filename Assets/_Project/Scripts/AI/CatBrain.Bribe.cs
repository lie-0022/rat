using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 앙심 후속 (design/cat-ideas/05, 2026-09-24).
    ///  뇌물: 쥐가 방금(10s) 내려놓은 치즈류가 고양이 정면 1.5m 안에 있으면 먹는다(4s) → 물건 사라짐 → 그 쥐 앙심 -50. 가치를 버려 목숨을 산다.
    ///  유인 속음: 털실이 던진 쥐에 귀속되면 놀이가 끝난 뒤 "속았다" +20. 이미 찍힌 쥐가 던진 유인엔 안 속는다.
    /// </summary>
    public partial class CatBrain
    {
        private CarryableItem _bribeItem;
        private ulong _bribeFrom;
        private ulong? _fooledBy;
        private CarryableItem[] _bribeScan;
        private float _nextBribeScan;

        /// <summary>테스트용 — 먹는 중인 뇌물.</summary>
        public bool IsEatingBribe => _bribeItem != null && State.Value == CatState.Distracted;

        // 순찰·복귀·의심·수색 틱에서 (추격 중엔 안 받는다)
        private bool TryEatBribe()
        {
            if (Time.time >= _nextBribeScan || _bribeScan == null)
            {
                _nextBribeScan = Time.time + 1f;
                _bribeScan = FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            }
            foreach (var item in _bribeScan)
            {
                if (item == null || !item.IsSpawned || item.Data == null || !item.Data.Has(ItemTrait.Edible)) continue;
                if (item.CarrierIds.Count > 0 || item.Pocketed.Value) continue;
                if (Time.time - item.LastReleaseTime > _balance.CatBribeRecentSeconds) continue; // 원래 놓여 있던 치즈는 뇌물이 아니다
                Vector3 to = item.transform.position - transform.position; to.y = 0f;
                if (to.magnitude > _balance.CatBribeRadius || Vector3.Angle(transform.forward, to) > 60f) continue;
                _bribeItem = item;
                _bribeFrom = item.LastCarrierId;
                Log.Dev($"고양이 [{name}]: 뇌물 발견 — {item.name} (client {_bribeFrom})");
                ServerDistract(item.transform.position, _balance.CatBribeEatSeconds, AttentionKind.Bribe);
                return true;
            }
            return false;
        }

        // TickDistracted: 뇌물이면 그 자리에서 먹는다(배회 안 함). 쥐가 도로 집어 가면 무효
        private bool TickBribe()
        {
            if (_bribeItem == null) return false;
            if (!_bribeItem.IsSpawned || _bribeItem.CarrierIds.Count > 0 || _bribeItem.Pocketed.Value)
            {
                Log.Dev($"고양이 [{name}]: 뇌물을 도로 가져감 — 무효");
                _bribeItem = null;
                _distractUntil = Time.time; // 바로 끝
                return true;
            }
            Vector3 to = _bribeItem.transform.position - transform.position; to.y = 0f;
            if (to.magnitude > 0.8f) { if (_movement.Velocity.sqrMagnitude < 0.01f) _movement.MoveTo(_bribeItem.transform.position, _balance.CatDistractedSpeed); }
            else { _movement.Stop(); if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to); }
            return true;
        }

        // Distracted가 끝날 때 (TickDistracted)
        private void FinishDistractExtras()
        {
            if (_bribeItem != null)
            {
                Log.Dev($"고양이 [{name}]: 냠냠 — {_bribeItem.name} 먹음");
                ServerAddGrudge(_bribeFrom, -_balance.CatGrudgeBribe, "뇌물");
                _bribeItem.NetworkObject.DespawnSafe();
                _bribeItem = null;
            }
            if (_fooledBy.HasValue)
            {
                ServerAddGrudge(_fooledBy.Value, _balance.CatGrudgeFooled, "털실에 속음");
                _fooledBy = null;
            }
        }
    }
}
