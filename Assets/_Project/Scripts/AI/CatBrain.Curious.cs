using System.Collections.Generic;
using RatGame.Core;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 호기심 앞발 (design/cat-ideas/03, 2026-09-24). 순찰·복귀 중 시야에 굴러가는 풀린 물건이 들어오면 가서 앞발로 3~5번 친다.
    /// 물리는 호스트 권한(규칙 1) — Rigidbody에 속도 변화만 준다. 다 치면 하품하고 복귀, 그 물건은 쿨다운 동안 무시.
    /// 같은 물건과 누적 30s 놀면 질려서 60s 무시 — 쥐가 무한히 캔을 굴려 붙잡아 두지 못하게.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>앞발을 칠 때마다 +1 (클라 연출용 — CatVisual이 변화를 보고 몸을 앞으로 튕긴다).</summary>
        public NetworkVariable<byte> PawTick = new NetworkVariable<byte>(0);

        private CarryableItem _curiousItem;
        private Rigidbody _curiousRb;
        private int _pawsLeft;
        private float _nextPawAt;
        private float _curiousSince;
        private float _reachDeadline;
        private float _yawnUntil = -1f;
        private Vector3 _curiousDest;
        private readonly Dictionary<CarryableItem, float> _curiousIgnoreUntil = new();
        private readonly Dictionary<CarryableItem, float> _boredom = new();

        /// <summary>지금 노는 물건 이름 (테스트·로그용).</summary>
        public string CuriousItemName => _curiousItem != null ? _curiousItem.name : "";

        private bool IsCuriosityAllowed(CarryableItem item) =>
            !_curiousIgnoreUntil.TryGetValue(item, out float until) || Time.time >= until;

        private bool CheckCuriosity()
        {
            var item = _senses.CuriosityTarget;
            if (item == null || DirIgnoreCuriosity) return false; // 귀환 카운트다운엔 안 속는다
            _curiousItem = item;
            _curiousRb = item.GetComponent<Rigidbody>();
            SetState(CatState.Curious);
            return true;
        }

        // SetState(Curious)에서 호출
        private void EnterCuriousState()
        {
            var range = _balance.CatPawCountRange;
            _pawsLeft = Random.Range(range.x, range.y + 1);
            _nextPawAt = 0f;
            _yawnUntil = -1f;
            _curiousSince = Time.time;
            _reachDeadline = Time.time + _balance.CatCuriousReachTimeout;
            _curiousDest = Vector3.positiveInfinity;
            _senses.IgnoreImpactNear = _curiousItem != null ? _curiousItem.transform : null;
            Log.Dev($"고양이 [{name}]: 호기심 — {CuriousItemName} (앞발 {_pawsLeft}번 예정)");
        }

        // SetState가 Curious 밖으로 나갈 때 호출 (다른 상태 사이 전이면 아무 일 없음)
        private void ExitCurious()
        {
            if (_curiousItem == null) return;
            var item = _curiousItem;
            float played = Time.time - _curiousSince;
            _boredom.TryGetValue(item, out float total);
            total += played;
            float ignore = _balance.CatCuriousCooldownSeconds;
            if (total >= _balance.CatBoredomSeconds)
            {
                ignore = _balance.CatBoredIgnoreSeconds;
                total = 0f;
                Log.Dev($"고양이 [{name}]: {item.name}에 질림 — {ignore}s 무시");
            }
            _boredom[item] = total;
            _curiousIgnoreUntil[item] = Time.time + ignore;
            _curiousItem = null;
            _curiousRb = null;
            _senses.IgnoreImpactNear = null;
        }

        private void TickCurious()
        {
            if (CheckStartle()) return;    // 바로 옆 깨짐·찍찍 → 펄쩍 (design/cat-ideas/11)
            if (CheckEscalation()) return; // 쥐 목격·의심 자극이 호기심보다 먼저
            var item = _curiousItem;
            if (item == null || !item.IsSpawned || _curiousRb == null || item.CarrierIds.Count > 0 || item.Pocketed.Value)
            {
                SetState(CatState.Return); // 쥐가 집어 갔거나 사라짐
                return;
            }
            if (_yawnUntil >= 0f)
            {
                if (Time.time >= _yawnUntil) SetState(CatState.Return);
                return;
            }

            Vector3 itemPos = item.transform.position;
            Vector3 flat = itemPos - transform.position; flat.y = 0f;
            if (flat.magnitude > _balance.CatPawReach)
            {
                if (Time.time >= _reachDeadline)
                {
                    Log.Dev($"고양이 [{name}]: {item.name}에 못 닿음 — 포기");
                    SetState(CatState.Return);
                    return;
                }
                // 굴러가는 물건을 따라간다 — 목적지는 0.4m 넘게 바뀔 때만 갱신
                Vector3 dest = CatMovement.Sample(itemPos, 1.5f, itemPos);
                if ((dest - _curiousDest).sqrMagnitude > 0.16f || _movement.Velocity.sqrMagnitude < 0.01f)
                {
                    _curiousDest = dest;
                    _movement.MoveTo(dest, _balance.CatCuriousSpeed);
                }
                return;
            }

            // 닿았다 — 멈춰서 마주 보고 앞발
            _reachDeadline = Time.time + _balance.CatCuriousReachTimeout;
            _movement.Stop();
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
            if (Time.time < _nextPawAt) return;
            Paw(flat);
        }

        private void Paw(Vector3 flatToItem)
        {
            // 정면 ±60° 랜덤 수평 + 살짝 위. 질량 무관 속도 변화 — 캔이든 포도알이든 비슷하게 굴러간다
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * flatToItem.normalized;
            Vector3 dv = dir * _balance.CatPawImpulse + Vector3.up * 0.35f;
            _curiousRb.WakeUp();
            _curiousRb.AddForce(dv, ForceMode.VelocityChange);
            PawTick.Value++;
            _pawsLeft--;
            _nextPawAt = Time.time + _balance.CatPawIntervalSeconds;
            Log.Dev($"고양이 [{name}]: 툭 — {_curiousItem.name} (남은 앞발 {_pawsLeft})");
            if (_pawsLeft > 0) return;
            _yawnUntil = Time.time + _balance.CatCuriousYawnSeconds;
            Log.Dev($"고양이 [{name}]: 하품 ({_balance.CatCuriousYawnSeconds}s)");
        }
    }
}
