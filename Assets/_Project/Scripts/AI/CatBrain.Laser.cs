using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 레이저 점 쫓기 (design/cat-ideas/13, 2026-09-24 고양이 41). World/LaserPointer가 점이 보이는 고양이에게 0.2s마다 부른다.
    /// 한가할 때만(의심·추격·놀이·잠·부재·싸움·실패·매복·상자 중엔 무시 — "관심과 의심은 다른 축") Distracted로 점을 달려서 쫓는다.
    /// 점이 안 오면 linger 뒤 원래 하던 일로(TickDistracted가 처리).
    /// </summary>
    public partial class CatBrain
    {
        public void ServerLaserDot(Vector3 dot, float linger)
        {
            if (!IsServer) return;
            var st = State.Value;
            if (st != CatState.Patrol && st != CatState.Return && st != CatState.Search && st != CatState.Curious && st != CatState.Distracted) return;
            bool entering = st != CatState.Distracted;
            _fooledBy = null;
            _wobbleAfterDistract = false;
            _distractPos = dot;
            _distractUntil = Time.time + linger;
            if (entering)
            {
                _stateBeforeDistract = st;
                SetState(CatState.Distracted);
                Log.Dev($"고양이 [{name}]: 빨간 점! 쫓아감");
            }
            Vector3 onMesh = CatMovement.Sample(dot, 2f, dot); // 벽·선반에 찍힌 점이면 그 아래 바닥으로
            _movement.MoveTo(onMesh, _balance.CatLaserChaseSpeed);
        }
    }
}
