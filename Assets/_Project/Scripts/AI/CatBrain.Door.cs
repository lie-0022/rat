using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 문 밀기 (design/cat-ideas/09, 2026-09-24 고양이 40). 문 쪽 판정·열림은 World/RoomDoor, 목적지 보정은 CatMovement.MoveTo가 한다.
    /// 여기는 미는 동안 고양이가 하던 일을 잊지 않게 붙잡는 것만.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>
        /// 호스트: 닫힌 문을 미는 중 (World/RoomDoor가 0.25s마다, 고양이 40). 미는 4s 동안 쫓던 것·조사하던 것을 잊지 않게
        /// 추격 놓침 타이머·의심·수색 시간·게이지 감쇠를 붙잡는다.
        /// </summary>
        public void ServerHoldAtDoor(float dt)
        {
            if (!IsServer) return;
            if (_lostSightSince >= 0f) _lostSightSince += dt;
            _suspiciousUntil = Mathf.Max(_suspiciousUntil, Time.time + 1f);
            _searchUntil = Mathf.Max(_searchUntil, Time.time + 1f);
            _senses.HoldGauge();
        }

    }
}
