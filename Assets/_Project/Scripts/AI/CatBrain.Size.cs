using RatGame.Core;
using Unity.Netcode;
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
        /// <summary>몸 배율 — 모든 클라가 읽는다(큰 고양이 발걸음 흔들림 등, 고양이 145). 스폰 전 초기값으로만 정한다.</summary>
        public NetworkVariable<float> BodyScale = new(1f);

        /// <summary>호스트: 스폰 전에만.</summary>
        public void ServerPrepareSize(float scale, int agentTypeId)
        {
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
            {
                SizeScale = scale;
                BodyScale = new NetworkVariable<float>(scale); // 스폰 전 .Value 쓰기는 NGO 경고 — 초기값으로 (고양이 127과 같은 방식)
                transform.localScale *= scale;
                _balance = _balance.ScaledForCat(scale);
                GetComponent<CatSenses>()?.ServerSetBalance(_balance);
            }
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agentTypeId != -1)
            {
                agent.agentTypeID = agentTypeId; // 번호는 음수일 수 있다
                // 에이전트 반지름·높이는 트랜스폼 배율이 곱해진다 — 그대로 두면 회피 반경이 몸(1.1)보다 큰 1.8이 되어 고양이끼리 밀치다
                // 목적지 0.8m 앞에서 서로 막혀 섰다(시험 56초). 구운 크기에 맞추고, 도착 판정도 몸 반지름의 반만큼 넉넉히
                var nav = UnityEngine.AI.NavMesh.GetSettingsByID(agentTypeId);
                float sx = Mathf.Max(0.01f, transform.localScale.x);
                agent.radius = nav.agentRadius / sx;
                agent.height = nav.agentHeight / sx;
                agent.stoppingDistance = nav.agentRadius * 0.5f;
            }
            Log.Dev($"고양이 크기: ×{SizeScale:0.##}, 에이전트 {(agent != null ? agent.agentTypeID : -1)}");
        }
    }
}
