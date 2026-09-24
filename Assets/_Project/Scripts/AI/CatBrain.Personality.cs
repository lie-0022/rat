using RatGame.Core;
using RatGame.Noise;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 성격 특이 행동 (design/cat-ideas/01, 2026-09-24).
    ///  겁쟁이: 큰 소리(원 loudness ≥ fleeLoudness — 깨짐·함정·쥐덫)를 들으면 소리 반대쪽으로 6m 도망(3s) → 그 뒤 소리 난 곳 조사.
    ///          자다가도 도망간다. 쥐 입장에선 접시 깨기가 "공격" — 가치를 버리는 트레이드.
    ///  (호기심쟁이의 특이 행동은 CatSenses 반응 속도 배율 + TickSuspicious에서 호기심 허용으로 처리)
    /// </summary>
    public partial class CatBrain
    {
        private const float FleeDistance = 6f;

        private void OnHeardForPersonality(NoiseEvent e, float heard)
        {
            var p = Personality;
            if (p == null || p.FleeLoudness <= 0f || e.Loudness < p.FleeLoudness) return;
            var st = State.Value;
            if (st is CatState.Chase or CatState.Capture or CatState.Toy or CatState.Away or CatState.Blunder or CatState.Fight) return;

            Vector3 away = transform.position - e.Pos; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            Vector3 dest = CatMovement.Sample(transform.position + away.normalized * FleeDistance, 2f, transform.position);
            _investigatePos = e.Pos;
            _senses.ConsumeStimulus();
            Log.Dev($"고양이 [{name}]: 으악! 도망 (소리 {e.Loudness:0}, {p.FleeSeconds}s)");
            EnterBlunder(CatBlunderKind.Flee, p.FleeSeconds);
            _movement.MoveTo(dest, 5f);
        }
    }
}
