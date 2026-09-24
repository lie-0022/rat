using RatGame.Core;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 가지고 놀기 (design/cat-ideas/04, 2026-09-24). Capture 명중 뒤, 움직일 수 있는 동료가 있으면 Downed 대신 Pinned로 붙잡고 논다.
    /// 루프: Bat(앞발로 툭툭) → 관심 감소 → Release(놓고 물러남, 쥐 Active) → 2m 도주하면 Chase, 아니면 다시 Pinned.
    /// 관심이 바닥나면 하품하고 풀어 준다(생존). 전체 시간이 끝나면 진짜 Downed. 다른 쥐가 눈에 띄면 그쪽을 쫓는다(미끼 = 구출).
    /// 물고 옮기기(Carry)는 다음 단계 — 소유 클라 위치를 매 프레임 밀어줘야 해서.
    /// </summary>
    public partial class CatBrain
    {
        private enum ToyPhase { Bat, Release, Yawn }

        private PlayerCondition _toyVictim;
        private ToyPhase _toyPhase;
        private float _toyPhaseEnd;
        private float _toyNextNudge;
        private float _toyInterest;
        private float _toyUntil;          // 전체 시간 끝 (Chase로 나갔다 다시 잡아도 이어짐)
        private float _toyLeftAt = -99f;  // Toy를 떠난 시각 — 재개 창 판단
        private ulong _toyVictimId;
        private Vector3 _toyReleasePos;

        /// <summary>테스트·HUD용.</summary>
        public float ToyInterest => _toyInterest;
        public string ToyPhaseName => State.Value == CatState.Toy ? _toyPhase.ToString() : "";

        // TickCapture 명중 시: 놀 수 있으면 Toy로(true), 아니면 호출자가 기존대로 Downed
        private bool TryStartToy(PlayerCondition victim)
        {
            if (!HasMobileTeammate(victim.OwnerClientId)) return false; // 솔로·전원 쓰러짐 직전이면 놀지 않는다
            bool resume = victim.OwnerClientId == _toyVictimId && Time.time - _toyLeftAt <= _balance.CatToyResumeWindow && Time.time < _toyUntil;
            if (!resume)
            {
                _toyInterest = _balance.CatToyInterest;
                _toyUntil = Time.time + _balance.CatToySeconds;
            }
            _toyVictim = victim;
            _toyVictimId = victim.OwnerClientId;
            victim.ServerSetState(ConditionState.Pinned);
            SetState(CatState.Toy);
            Log.Dev($"고양이 [{name}]: 가지고 놀기 {(resume ? "재개" : "시작")} — client {_toyVictimId} (관심 {_toyInterest:0}, 남은 {_toyUntil - Time.time:0}s)");
            return true;
        }

        private bool HasMobileTeammate(ulong victimId)
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.ClientId == victimId || client.PlayerObject == null) continue;
                var c = client.PlayerObject.GetComponent<PlayerCondition>();
                if (c == null) continue;
                var s = c.State.Value;
                if (s == ConditionState.Active || s == ConditionState.Hidden || s == ConditionState.Stunned) return true;
            }
            return false;
        }

        // SetState(Toy)에서 호출
        private void EnterToyState()
        {
            _movement.Stop();
            BeginBat();
        }

        private void BeginBat()
        {
            _toyPhase = ToyPhase.Bat;
            _toyPhaseEnd = Time.time + _balance.CatToyBatSeconds;
            _toyNextNudge = Time.time + 0.5f;
        }

        // SetState가 Toy 밖으로 나갈 때 — 어떤 길로 나가든 잡힌 쥐가 Pinned로 남지 않게
        private void ExitToy()
        {
            _toyLeftAt = Time.time;
            if (_toyVictim != null && _toyVictim.State.Value == ConditionState.Pinned)
                _toyVictim.ServerSetState(ConditionState.Active);
            _toyVictim = null;
        }

        private void TickToy()
        {
            var victim = _toyVictim;
            if (victim == null || !victim.IsSpawned) { SetState(CatState.Return); return; }

            // 미끼: 다른 쥐가 보이면 잡은 쥐를 놓고 그쪽으로 (구출)
            var other = _senses.VisibleTarget;
            if (other != null && other != victim && _toyPhase != ToyPhase.Yawn)
            {
                Log.Dev($"고양이 [{name}]: 다른 쥐 발견 — client {_toyVictimId} 놓고 client {other.OwnerClientId} 추격");
                _chaseTarget = other;
                SetState(CatState.Chase); // ExitToy가 잡힌 쥐를 풀어 준다
                return;
            }
            // 깨짐·찍찍·함정 같은 큰 소리는 흥을 깬다
            if (_senses.HasNewStimulus)
            {
                if (_senses.ImmediateInvestigate) { _toyInterest -= _balance.CatToyDistractDrain; Log.Dev($"고양이 [{name}]: 소리에 흥 깸 (관심 {_toyInterest:0})"); }
                _senses.ConsumeStimulus();
            }

            if (_toyPhase != ToyPhase.Yawn && Time.time >= _toyUntil)
            {
                // 시간 끝 — 진짜 다운. 놓아준 창이어도 아직 도망 못 쳤으면(2m 안 — 벗어났다면 이미 Chase) 다운.
                // 고양이와의 거리로 판정하면 물러선 만큼 빠져나가 새 놀이가 30s씩 무한 반복됐다
                if (victim.State.Value == ConditionState.Pinned || victim.State.Value == ConditionState.Active)
                    victim.ServerSetState(ConditionState.Downed);
                Log.Dev($"고양이 [{name}]: 놀이 끝 — client {_toyVictimId} 다운");
                _toyVictim = null;
                SetState(CatState.Return);
                return;
            }

            switch (_toyPhase)
            {
                case ToyPhase.Bat:
                    FaceTowards(victim.transform.position);
                    if (Time.time >= _toyNextNudge)
                    {
                        _toyNextNudge = Time.time + 1f;
                        Nudge(victim);
                    }
                    if (Time.time < _toyPhaseEnd) return;
                    _toyInterest -= _balance.CatToyLoopDrain;
                    if (_toyInterest <= 0f)
                    {
                        _toyPhase = ToyPhase.Yawn;
                        _toyPhaseEnd = Time.time + _balance.CatToyYawnSeconds;
                        victim.ServerSetState(ConditionState.Active); // 질렸다 — 살아남음
                        // 하품 동안 + 조금 더 못 본 척 — 바로 옆이라 놓자마자 다시 덮치던 문제
                        _senses.IgnorePlayer(_toyVictimId, Time.time + _balance.CatToyYawnSeconds + _balance.CatToyBoredIgnoreSeconds);
                        Log.Dev($"고양이 [{name}]: 흥미 잃음 — client {_toyVictimId} 풀어 줌(하품)");
                        return;
                    }
                    // 놓아주기 — 한 발 물러난다
                    _toyPhase = ToyPhase.Release;
                    _toyPhaseEnd = Time.time + _balance.CatToyReleaseSeconds;
                    _toyReleasePos = victim.transform.position;
                    victim.ServerSetState(ConditionState.Active);
                    Vector3 back = transform.position - victim.transform.position; back.y = 0f;
                    if (back.sqrMagnitude < 0.01f) back = -transform.forward;
                    _movement.MoveTo(CatMovement.Sample(transform.position + back.normalized, 1f, transform.position), _balance.CatPatrolSpeed);
                    Log.Dev($"고양이 [{name}]: 놓아줌 (관심 {_toyInterest:0}, {_balance.CatToyReleaseSeconds}s)");
                    return;

                case ToyPhase.Release:
                    FaceTowards(victim.transform.position);
                    if (victim.State.Value != ConditionState.Active) { SetState(CatState.Return); return; } // 숨었거나 다른 일
                    if (Vector3.Distance(victim.transform.position, _toyReleasePos) >= _balance.CatToyEscapeDistance)
                    {
                        Log.Dev($"고양이 [{name}]: 도망친다 — 덮치러");
                        _chaseTarget = victim;
                        SetState(CatState.Chase);
                        return;
                    }
                    if (Time.time < _toyPhaseEnd) return;
                    victim.ServerSetState(ConditionState.Pinned); // 안 도망쳤다 — 다시 앞발로
                    _movement.Stop();
                    BeginBat();
                    return;

                case ToyPhase.Yawn:
                    if (Time.time >= _toyPhaseEnd) { _toyVictim = null; SetState(CatState.Return); }
                    return;
            }
        }

        private void FaceTowards(Vector3 pos)
        {
            Vector3 d = pos - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        // 앞발로 툭 — 잡힌 쥐를 옆으로 조금 민다. 이동은 소유 클라 권한이라 소유자에게 순간이동 RPC (docs/03 예외)
        private void Nudge(PlayerCondition victim)
        {
            var pc = victim.GetComponent<PlayerController>();
            if (pc == null) return;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f) * transform.forward; dir.y = 0f;
            Vector3 target = victim.transform.position + dir.normalized * _balance.CatToyNudge;
            Vector3 onMesh = CatMovement.Sample(target, 0.8f, victim.transform.position);
            onMesh.y = victim.transform.position.y;
            pc.TeleportClientRpc(onMesh, victim.transform.rotation, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { victim.OwnerClientId } }
            });
            PawTick.Value++; // 앞발 연출 재사용 (CatVisual 몸 튐)
        }
    }
}
