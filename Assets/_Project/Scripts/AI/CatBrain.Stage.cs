namespace RatGame.AI
{
    /// <summary>
    /// 스테이지 조건이 고양이에게 주는 배율 (고양이 106 "고양이 간식 날"). 성격 속도에 곱한다 — 역할(문지기·순찰꾼)로 성격이 바뀌어도 유지.
    /// </summary>
    public partial class CatBrain
    {
        private float _stageSpeedMul = 1f;

        /// <summary>호스트: 이번 스테이지 이동 속도 배율.</summary>
        public void ServerSetStageSpeed(float multiplier)
        {
            if (!IsServer) return;
            _stageSpeedMul = multiplier;
            ApplyPersonality();
        }
    }
}
