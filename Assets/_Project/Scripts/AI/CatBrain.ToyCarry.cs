using RatGame.Core;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 가지고 놀기 — 물고 옮기기 부분 (design/cat-ideas/04). 입 자리·쥐 위치 밀어 주기 포함.
    /// CatBrain.Toy.cs가 300줄을 넘어 나눔 (고양이 168).
    /// </summary>
    public partial class CatBrain
    {
        private Vector3 _carryDest;
        private float _carryNextPush;
        private float _carryNextDropRoll;

        private bool TryBeginCarry(PlayerCondition victim)
        {
            if (_toyCarried) return false;
            _toyCarried = true; // 굴림은 한 번만 — 실패해도 이번 판엔 다시 안 굴린다
            if (Random.value >= _balance.CatToyCarryChance) return false;
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_spots[i].Type != CatSpotType.Bed && _spots[i].Type != CatSpotType.Sun) continue;
                float d = Vector3.Distance(transform.position, _spots[i].Pos);
                if (d < _balance.CatToyCarryMinDistance || d > _balance.CatToyCarryMaxDistance || d >= bestD) continue;
                bestD = d; best = i;
            }
            if (best < 0) return false;
            _carryDest = _spots[best].Pos;
            _toyPhase = ToyPhase.Carry;
            _toyPhaseEnd = Time.time + _balance.CatToyCarrySeconds;
            _carryNextPush = 0f;
            _carryNextDropRoll = Time.time + 1f;
            _movement.MoveTo(_carryDest, _balance.CatPatrolSpeed * _balance.CatToyCarrySpeedMul);
            Log.Dev($"고양이 [{name}]: 물고 감 — client {_toyVictimId} → {_spots[best].Name} ({bestD:0.0}m)");
            return true;
        }

        private void TickCarry(PlayerCondition victim)
        {
            // 입에 문 쥐 — 소유 클라에 위치를 밀어 준다 (Nudge와 같은 docs/03 예외, 10Hz)
            if (Time.time >= _carryNextPush)
            {
                _carryNextPush = Time.time + 0.1f;
                // 걷는 동안엔 쥐 위치가 한 박자 늦게 도착해 몸 속으로 밀렸다 — 걸음만큼 앞당겨 준다 (고양이 167)
                PushVictim(victim, MouthPoint(victim) + _movement.Velocity * CarryLeadSeconds);
            }
            if (Time.time >= _carryNextDropRoll)
            {
                _carryNextDropRoll = Time.time + 1f;
                if (Random.value < _balance.CatToyCarryDropChance)
                {
                    Log.Dev($"고양이 [{name}]: 앗 — 떨어뜨림! (client {_toyVictimId})");
                    BeginRelease(victim, false); // 공짜 도주 창
                    return;
                }
            }
            if (Vector3.Distance(transform.position, _carryDest) > 0.8f && Time.time < _toyPhaseEnd) return;
            // 도착 — 내려놓고 자기 자리에서 계속 논다
            PushVictim(victim, MouthPoint(victim));
            Log.Dev($"고양이 [{name}]: 내려놓음 ({Vector3.Distance(transform.position, _carryDest):0.0}m 남음)");
            _movement.Stop();
            BeginBat();
        }

        private const float MouthClearance = 0.4f; // 쥐 반지름 0.3 + 여유
        private const float CarryLeadSeconds = 0.2f;

        private static readonly float[] MouthTries = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        private Vector3 MouthPoint(PlayerCondition victim)
        {
            // 몸통 충돌체 바로 앞(쥐 반지름 + 여유) — 0.6 고정이면 큰 고양이·키운 충돌체 속에 놓여 튕겼다 (고양이 167)
            // 앞이 벽이면 몸 둘레로 — 바닥이 없다고 고양이 위치를 대신 쓰면 쥐가 고양이 한가운데 놓였다 (고양이 179)
            float front = FrontDistance(MouthClearance);
            float minDist = front - 0.1f; // 몸 반지름 + 쥐 반지름 이상
            for (int i = 0; i < MouthTries.Length; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, MouthTries[i], 0f) * transform.forward;
                if (!UnityEngine.AI.NavMesh.SamplePosition(transform.position + dir * front, out var hit, 0.8f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                Vector3 flat = hit.position - transform.position; flat.y = 0f;
                if (flat.magnitude < minDist) continue; // 끌려 들어온 점도 몸 속
                if (i > 0) MouthFallbacks++;
                Vector3 p = hit.position;
                p.y = victim.transform.position.y;
                return p;
            }
            MouthFallbacks++;
            return victim.transform.position; // 둘 데가 없으면 쥐를 그대로
        }

        /// <summary>테스트용 — 입 자리를 앞이 아닌 곳(또는 제자리)으로 잡은 횟수.</summary>
        public int MouthFallbacks { get; private set; }

        private static void PushVictim(PlayerCondition victim, Vector3 pos)
        {
            var pc = victim.GetComponent<PlayerController>();
            if (pc == null) return;
            pc.TeleportClientRpc(pos, victim.transform.rotation, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { victim.OwnerClientId } }
            });
        }
    }
}
