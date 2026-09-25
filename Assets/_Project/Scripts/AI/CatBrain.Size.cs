using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 큰 고양이 (고양이 141 — 실제 비율, 고양이 키 = 쥐 3배). 벽 속 맵이 스폰 전에 크기·에이전트 타입을 정한다.
    /// 몸 배율만큼 몸·공간 거리(잡기·앞발·시야·수색…)를 늘린 균형값 복사본을 이 고양이에만 준다. 속도는 그대로.
    /// 크기는 NetworkTransform이 스케일을 동기화해 클라에도 간다. 창고 데모 고양이는 예전 크기 그대로(1).
    /// </summary>
    public partial class CatBrain
    {
        public float SizeScale { get; private set; } = 1f;

        /// <summary>호스트: 스폰 전에만.</summary>
        public void ServerPrepareSize(float scale, int agentTypeId)
        {
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
            {
                SizeScale = scale;
                transform.localScale *= scale;
                _balance = _balance.ScaledForCat(scale);
                GetComponent<CatSenses>()?.ServerSetBalance(_balance);
            }
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agentTypeId != -1) agent.agentTypeID = agentTypeId; // 번호는 음수일 수 있다
            Log.Dev($"고양이 크기: ×{SizeScale:0.##}, 에이전트 {(agent != null ? agent.agentTypeID : -1)}");
        }
    }
}
