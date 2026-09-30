using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>유인 자극 종류 — 관심 점수표(BalanceConfigSO.attentionScores)의 순서 (design/cat-ideas/13).</summary>
    public enum AttentionKind { Bribe, String, Yarn, Laser, Catnip }

    /// <summary>
    /// 관심 점수 (design/cat-ideas/13 본체, 고양이 261, 호스트). 유인은 모두 한 저울에 — 유인 중(Distracted)엔 지금 것보다 점수가 낮은 자극을 무시한다
    /// (캣닢에 취한 고양이는 레이저를 안 본다). 같은 종류에 짧은 시간 여러 번 반응하면 지루해져 시간이 준다(무한 유인 방지).
    /// 의심·추격 우선은 그대로 — ServerDistract·ServerLaserDot의 상태 조건이 먼저 거른다.
    /// </summary>
    public partial class CatBrain
    {
        private float _attentionScore;
        private readonly Dictionary<AttentionKind, List<float>> _attentionHistory = new();

        /// <summary>유인 아이템 진입점 (docs/07 — Chase보다 우선순위 낮음). 태스크 1-6에서 호출.</summary>
        public void ServerDistract(Vector3 pos, float seconds, AttentionKind kind, bool wobbleAfter = false, ulong? fooledBy = null)
        {
            if (!IsServer || State.Value == CatState.Chase || State.Value == CatState.Capture || State.Value == CatState.Toy) return;
            // 찍힌 쥐가 던진 유인엔 안 속는다 — "네가 던진 거잖아" (design/cat-ideas/05)
            if (fooledBy.HasValue && IsGrudged(fooledBy.Value)) { Log.Dev($"고양이 [{name}]: client {fooledBy.Value}의 유인엔 안 속음"); return; }
            if (!AttentionWins(kind, out float score)) return; // 더 센 관심에 꽂혀 있음 (고양이 261)
            seconds *= BoredomScale(kind);
            _attentionScore = score;
            _fooledBy = fooledBy;
            _distractPos = pos;
            _wobbleAfterDistract = wobbleAfter; // 캣닢 — 끝나면 비틀거림 (design/cat-ideas/11)
            _distractUntil = Time.time + seconds;
            _stateBeforeDistract = State.Value == CatState.Distracted ? _stateBeforeDistract : State.Value;
            SetState(CatState.Distracted);
        }

        /// <summary>이 자극에 반응할지 — 지금 유인 중이고 더 센 것에 꽂혀 있으면 false.</summary>
        private bool AttentionWins(AttentionKind kind, out float score)
        {
            score = _balance.AttentionScore((int)kind);
            if (State.Value == CatState.Distracted && Time.time < _distractUntil && score < _attentionScore)
            {
                Log.Dev($"고양이 [{name}]: {kind}({score}) 무시 — 더 센 관심({_attentionScore})에 꽂힘");
                return false;
            }
            return true;
        }

        /// <summary>지루함 — 같은 종류에 창 안에서 boredomCount번 넘게 반응했으면 반응 시간 배율.</summary>
        private float BoredomScale(AttentionKind kind)
        {
            if (!_attentionHistory.TryGetValue(kind, out var times)) _attentionHistory[kind] = times = new List<float>();
            float now = Time.time;
            times.RemoveAll(t => now - t > _balance.BoredomWindowSeconds);
            times.Add(now);
            if (times.Count <= _balance.BoredomCount) return 1f;
            Log.Dev($"고양이 [{name}]: {kind} 지루함 ({times.Count}번째) — 시간 ×{_balance.BoredomMultiplier}");
            if (times.Count == _balance.BoredomCount + 1) CatCueClientRpc((byte)CatCueKind.Bored, transform.position); // 처음 질린 순간 한 번 — 규칙을 알게 (고양이 285)
            return _balance.BoredomMultiplier;
        }
    }
}
