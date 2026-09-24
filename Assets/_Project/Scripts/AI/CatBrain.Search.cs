using System.Collections.Generic;
using RatGame.Core;
using RatGame.Player;
using RatGame.World;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 수색 상태 (design/cat-ideas/14, 2026-09-24). 추격 중 타깃을 놓친 뒤 마지막 목격점 반경 안의 HideSpot을
    /// 가까운 순으로 돌며 킁킁(2s) → 확률로 건드려 안의 쥐를 끄집어낸다. 발각된 쥐에게는 튀어나갈 틈(Pounce 창)을 준 뒤 추격.
    /// 외부 자극(미끼·소음·목격)에 끊긴다. 시간·스팟 수를 다 쓰면 하품하고 Return.
    /// </summary>
    public partial class CatBrain
    {
        private HideSpot[] _hideSpots;
        private readonly List<HideSpot> _searchQueue = new();
        private HideSpot _searchTarget;      // 지금 가는/킁킁거리는 스팟
        private float _searchUntil;
        private float _sniffUntil = -1f;     // -1 = 아직 도착 전
        private PlayerCondition _pounceTarget;
        private float _pounceUntil;
        private Vector3 _searchCenter;

        /// <summary>수색 중인 스팟 이름 (테스트·HUD 로그용).</summary>
        public string SearchTargetName => _searchTarget != null ? _searchTarget.DisplayName : "";

        private void CollectHideSpots()
        {
            _hideSpots = FindObjectsByType<HideSpot>(FindObjectsSortMode.None);
            Log.Dev($"고양이 [{name}]: 숨을 곳 {_hideSpots.Length}개");
        }

        /// <summary>마지막 목격점 반경 안에 숨을 곳이 있으면 수색 시작. 없으면 false(호출자가 Suspicious로).</summary>
        private bool TryEnterSearch(Vector3 center)
        {
            if (_hideSpots == null || _hideSpots.Length == 0) return false;
            var near = new List<(float d, HideSpot s)>();
            foreach (var s in _hideSpots)
            {
                if (s == null || !s.IsSpawned) continue;
                float d = Vector3.Distance(center, s.transform.position);
                if (d <= _balance.CatSearchRadius) near.Add((d, s));
            }
            if (near.Count == 0) return false;
            near.Sort((a, b) => a.d.CompareTo(b.d));
            _searchQueue.Clear();
            for (int i = 0; i < Mathf.Min(_balance.CatSearchMaxSpots, near.Count); i++) _searchQueue.Add(near[i].s);
            _searchCenter = center;
            SetState(CatState.Search);
            return true;
        }

        // SetState(Search)에서 호출
        private void EnterSearchState()
        {
            TargetClientId.Value = 0;
            _chaseTarget = null;
            _pounceTarget = null;
            _searchUntil = Time.time + _balance.CatSearchSeconds;
            Log.Dev($"고양이 [{name}]: 수색 시작 — 후보 {_searchQueue.Count}개 (반경 {_balance.CatSearchRadius}m, {_balance.CatSearchSeconds}s)");
            NextSearchSpot();
        }

        private void NextSearchSpot()
        {
            _sniffUntil = -1f;
            if (_searchQueue.Count == 0) { _searchTarget = null; return; }
            _searchTarget = _searchQueue[0];
            _searchQueue.RemoveAt(0);
            _movement.MoveTo(CatMovement.Sample(_searchTarget.ExitPosition, 1.5f, _searchTarget.transform.position), _balance.CatSuspiciousSpeed);
        }

        private void TickSearch()
        {
            // 발각 뒤 튀어나갈 틈 — 이 사이엔 감각 판정을 쉰다. 틈이 끝나면 추격(코앞이면 곧 Capture)
            if (_pounceTarget != null)
            {
                if (Time.time < _pounceUntil) return;
                _chaseTarget = _pounceTarget;
                _pounceTarget = null;
                if (_chaseTarget.State.Value == ConditionState.Active) SetState(CatState.Chase);
                else SetState(CatState.Return);
                return;
            }
            if (CheckEscalation()) return; // 미끼·소음·목격이 수색을 끊는다
            if (Time.time >= _searchUntil || _searchTarget == null)
            {
                Log.Dev($"고양이 [{name}]: 수색 끝 (하품)");
                SetState(CatState.Return);
                return;
            }

            if (_sniffUntil < 0f)
            {
                if (!_movement.Arrived) return;
                // 도착 — 코를 대고 킁킁. 여럿이 숨었으면 냄새가 진해 더 오래
                int occupants = _searchTarget.OccupantCount.Value;
                float mul = occupants > 1 ? occupants * _balance.CatMultiHideSniffMul : 1f;
                _sniffUntil = Time.time + _balance.CatSniffSeconds * mul;
                _movement.Stop();
                Vector3 face = _searchTarget.transform.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
                Log.Dev($"고양이 [{name}]: 킁킁 {_searchTarget.DisplayName} ({_sniffUntil - Time.time:0.0}s, 안에 {occupants}명)");
                return;
            }
            if (Time.time < _sniffUntil) return;

            // 킁킁 끝 — 확률로 건드린다(신발 뒤집기). 안에 쥐가 있으면 발각 → Pounce 창
            if (Random.value < _balance.CatDisturbChance)
            {
                var ejected = _searchTarget.ServerEject();
                Log.Dev($"고양이 [{name}]: {_searchTarget.DisplayName} 건드림 — {(ejected.Count > 0 ? $"발각 {ejected.Count}명!" : "비었음")}");
                if (ejected.Count > 0)
                {
                    _pounceTarget = ejected[0];
                    _pounceUntil = Time.time + _balance.CatPounceWindowSeconds;
                    return;
                }
            }
            NextSearchSpot();
            if (_searchTarget == null)
            {
                Log.Dev($"고양이 [{name}]: 수색 끝 — 스팟 소진");
                SetState(CatState.Return);
            }
        }
    }
}
