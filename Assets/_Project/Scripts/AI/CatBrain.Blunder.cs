using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    public enum CatBlunderKind : byte { None, Slip, Stun, Startle, Wobble, Hairball /* 그루밍 뒤 웩웩 (2026-09-24) */, Flee /* 겁쟁이 — 큰 소리에 도망 */, Stretch /* 게으름뱅이 기지개 */ }

    /// <summary>
    /// 댕청한 실패 (design/cat-ideas/11, 2026-09-24). 쥐가 만든 상황에서만 확실히 — 무작위 실패는 억울하지도 웃기지도 않다.
    ///  Slip: 속도 ≥4로 달리다 바닥의 Slippery 물건(비누·접시·수박)을 밟음 → 3m 쭉 → 벽에 박으면 기절 2s.
    ///  Startle: 호기심으로 놀다가 바로 옆에서 깨짐·찍찍·함정 → 뒤로 펄쩍 → 소리 난 곳 조사.
    ///  Wobble: 캣닢 놀이가 끝나면 10s 비틀비틀(속도 ×0.5, 감각 ×0.3).
    /// 실패는 앙심을 올리지 않는다.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>지금 어떤 실패 중인지 (클라 연출 — CatVisual).</summary>
        public NetworkVariable<CatBlunderKind> BlunderKind = new NetworkVariable<CatBlunderKind>(CatBlunderKind.None);

        private float _blunderUntil;
        private float _slipCooldownUntil;
        private Vector3 _slipDir;
        private float _slipSpeed;
        private float _slipTraveled;
        private Vector3 _startleFrom;
        private float _nextWobbleStep;
        private bool _wobbleAfterDistract;
        private CarryableItem[] _slipItems;
        private float _nextSlipScan;

        [SerializeField] private LootItemSO _hairballItem; // 헤어볼 전리품 (Cat 프리팹에서 연결)

        /// <summary>헤어볼 몇 개 뱉었나 (테스트용).</summary>
        public int HairballsSpawned { get; private set; }

        // Groom 스팟 머무름이 끝날 때 (TickPatrol): 가끔 웩웩
        private bool TryHairball()
        {
            if (_hairballItem == null || _hairballItem.Prefab == null) return false;
            if (Random.value >= _balance.CatHairballChance) return false;
            Log.Dev($"고양이 [{name}]: 웩웩…");
            EnterBlunder(CatBlunderKind.Hairball, _balance.CatHairballSeconds);
            return true;
        }

        private void SpawnHairball()
        {
            Vector3 pos = transform.position + transform.forward * 0.6f + Vector3.up * 0.3f;
            var go = Instantiate(_hairballItem.Prefab, pos, Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn(true); // 씬과 함께 사라지게
            HairballsSpawned++;
            Log.Dev($"고양이 [{name}]: 헤어볼 퉤 ({pos:F1})");
        }

        /// <summary>테스트용 — 이번 미끄러짐에서 실제로 밀린 거리.</summary>
        public float LastSlipDistance => _slipTraveled;

        // ---- 진입 ----

        // Update에서 매 프레임 (호스트): 빠르게 달리다 미끄러운 물건을 밟았나
        private void CheckSlip()
        {
            var st = State.Value;
            if (st == CatState.Blunder || st == CatState.Sleep || st == CatState.Toy || st == CatState.Capture) return;
            if (Time.time < _slipCooldownUntil) return;
            Vector3 v = _movement.Velocity; v.y = 0f;
            if (v.magnitude < _balance.CatSlipMinSpeed) return;

            if (Time.time >= _nextSlipScan || _slipItems == null)
            {
                _nextSlipScan = Time.time + 2f;
                _slipItems = FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            }
            // 엎은 물그릇 웅덩이 — 바닥 자체가 미끄럽다 (design/cat-ideas/06 2단계)
            foreach (var puddle in WaterBowl.ActivePuddles)
            {
                if (puddle == null || !puddle.PuddleActive.Value || !puddle.Contains(transform.position)) continue;
                StartSlip(v, null);
                return;
            }
            float r2 = _balance.CatSlipDetectRadius * _balance.CatSlipDetectRadius;
            foreach (var item in _slipItems)
            {
                if (item == null || !item.IsSpawned || item.Data == null || !item.Data.Has(ItemTrait.Slippery)) continue;
                if (item.CarrierIds.Count > 0 || item.Pocketed.Value) continue;
                Vector3 d = item.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                if (item.transform.position.y > transform.position.y + 0.6f) continue; // 선반 위 물건은 못 밟는다
                StartSlip(v, item);
                return;
            }
        }

        private void StartSlip(Vector3 velocity, CarryableItem item)
        {
            _slipDir = velocity.normalized;
            _slipSpeed = _balance.CatSlipDistance / Mathf.Max(0.1f, _balance.CatSlipSeconds) * 2f; // 선형 감속 — 평균 속도 = 거리/시간
            _slipTraveled = 0f;
            var rb = item != null ? item.GetComponent<Rigidbody>() : null;
            if (rb != null) { rb.WakeUp(); rb.AddForce(_slipDir * 3f + Vector3.up * 0.5f, ForceMode.VelocityChange); } // 밟은 비누도 튕겨 나간다
            Log.Dev($"고양이 [{name}]: 미끄러짐! {(item != null ? item.name + " 밟음" : "젖은 바닥")} (속도 {velocity.magnitude:0.0})");
            EnterBlunder(CatBlunderKind.Slip, _balance.CatSlipSeconds);
        }

        // TickCurious 맨 앞: 놀다가 바로 옆에서 큰 소리 → 펄쩍
        private bool CheckStartle()
        {
            if (!_senses.HasNewStimulus || !_senses.ImmediateInvestigate) return false;
            if (Vector3.Distance(_senses.LastStimulusPos, transform.position) > _balance.CatStartleRadius) return false;
            _startleFrom = _senses.LastStimulusPos;
            _investigatePos = _senses.LastStimulusPos;
            _senses.ConsumeStimulus();
            Log.Dev($"고양이 [{name}]: 깜짝! 펄쩍");
            EnterBlunder(CatBlunderKind.Startle, _balance.CatStartleSeconds);
            return true;
        }

        private void EnterBlunder(CatBlunderKind kind, float seconds)
        {
            BlunderKind.Value = kind;
            _blunderUntil = Time.time + seconds;
            _nextWobbleStep = 0f;
            if (State.Value != CatState.Blunder) SetState(CatState.Blunder);
            if (kind == CatBlunderKind.Wobble) _senses.SensitivityMultiplier = 0.3f; // 취함
            else if (kind != CatBlunderKind.Flee) _movement.Stop(); // 도망은 달린다
        }

        // SetState가 Blunder 밖으로 나갈 때
        private void ExitBlunder()
        {
            if (BlunderKind.Value != CatBlunderKind.None) BlunderKind.Value = CatBlunderKind.None;
            _slipCooldownUntil = Time.time + _balance.CatSlipCooldown;
        }

        // ---- 틱 ----

        private void TickBlunder()
        {
            switch (BlunderKind.Value)
            {
                case CatBlunderKind.Slip:
                {
                    float t = 1f - Mathf.Clamp01((_blunderUntil - Time.time) / Mathf.Max(0.1f, _balance.CatSlipSeconds));
                    float speed = _slipSpeed * (1f - t);
                    float want = speed * Time.deltaTime;
                    float got = _movement.Shove(_slipDir * want);
                    _slipTraveled += got;
                    if (want > 0.02f && got < want * 0.5f)
                    {
                        // 벽·상자에 쿵 — 기절
                        Log.Dev($"고양이 [{name}]: 쿵! 벽에 박음 ({_slipTraveled:0.0}m 미끄러짐) — 기절 {_balance.CatSlipStunSeconds}s");
                        EnterBlunder(CatBlunderKind.Stun, _balance.CatSlipStunSeconds);
                        return;
                    }
                    if (Time.time < _blunderUntil) return;
                    Log.Dev($"고양이 [{name}]: 미끄러짐 끝 ({_slipTraveled:0.0}m) — 추스르기");
                    EnterBlunder(CatBlunderKind.Stun, _balance.CatSlipRecoverSeconds);
                    return;
                }
                case CatBlunderKind.Stun:
                    if (Time.time >= _blunderUntil) SetState(CatState.Return); // 보이면 Return에서 곧 다시 쫓는다
                    return;

                case CatBlunderKind.Startle:
                {
                    // 처음 0.3s 동안 소리 반대쪽으로 펄쩍
                    float elapsed = _balance.CatStartleSeconds - (_blunderUntil - Time.time);
                    if (elapsed < 0.3f)
                    {
                        Vector3 away = transform.position - _startleFrom; away.y = 0f;
                        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                        _movement.Shove(away.normalized * (1.2f / 0.3f) * Time.deltaTime);
                    }
                    if (Time.time < _blunderUntil) return;
                    SetState(CatState.Suspicious); // 뭐였지? 소리 난 곳 조사
                    return;
                }
                case CatBlunderKind.Stretch:
                    if (Time.time < _blunderUntil) return;
                    _senses.RaiseGaugeTo(_balance.CatSuspicionThreshold, _investigatePos); // 기지개 동안 식은 게이지 — 조사는 한다
                    SetState(CatState.Suspicious);
                    return;

                case CatBlunderKind.Flee:
                    if (Time.time < _blunderUntil) return;
                    Log.Dev($"고양이 [{name}]: 뭐였지… 조사");
                    _senses.RaiseGaugeTo(_balance.CatSuspicionThreshold, _investigatePos); // 도망 동안 식은 게이지 — 조사는 한다
                    SetState(CatState.Suspicious); // 소리 난 곳 조사 (_investigatePos)
                    return;

                case CatBlunderKind.Hairball:
                    if (Time.time < _blunderUntil) return;
                    SpawnHairball();
                    SetState(CatState.Return);
                    return;

                case CatBlunderKind.Wobble:
                    if (CheckEscalation()) return; // 취해도 코앞 쥐는 쫓는다
                    if (Time.time >= _blunderUntil) { Log.Dev($"고양이 [{name}]: 술 깸"); SetState(CatState.Return); return; }
                    if (Time.time >= _nextWobbleStep)
                    {
                        _nextWobbleStep = Time.time + 1f;
                        _movement.MoveTo(_movement.RandomPointAround(transform.position, 2f), _balance.CatPatrolSpeed * _balance.CatWobbleSpeedMul);
                    }
                    return;

                default:
                    SetState(CatState.Return);
                    return;
            }
        }
    }
}
