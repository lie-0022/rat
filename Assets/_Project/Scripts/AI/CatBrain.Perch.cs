using RatGame.Core;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 선반 관찰대 + 점프 실패 (design/cat-ideas/09·01·11, 2026-09-24 고양이 39).
    ///  Perch 스팟(선반 위)에 도착하면 오래 머물며 멀리 본다(시야 거리 12 — 눈높이가 높아 상자 너머도). 선반 밑은 판이 가려 안 보인다.
    ///  올라가는 점프는 가끔 실패 — 머리를 박고 떨어져 잠깐 멍하다(Blunder Stun).
    /// </summary>
    public partial class CatBrain
    {
        private bool _perched;

        /// <summary>선반 위에서 내려다보는 중 (테스트·디버그).</summary>
        public bool IsPerched => _perched && _dwelling;

        // OnNetworkSpawn(호스트)에서
        private void InitJump()
        {
            _movement.JumpSeconds = _balance.CatJumpSeconds;
            _movement.JumpFailCheck = () => Random.value < _balance.CatJumpFailChance;
            _movement.JumpFailed += OnJumpFailed;
        }

        private void OnJumpFailed()
        {
            Log.Dev($"고양이 [{name}]: 쿵 — 점프 실패! 떨어짐");
            EnterBlunder(CatBlunderKind.Stun, _balance.CatJumpFailStunSeconds); // Stun 끝나면 Return
        }

        private void EnterPerch()
        {
            _perched = true;
            _senses.ViewDistanceOverride = _balance.CatPerchViewDistance;
            Dwell(_balance.CatPerchSeconds, 1f);
            Log.Dev($"고양이 [{name}]: 선반 위 — 내려다봄 (시야 {_balance.CatPerchViewDistance}m, {_balance.CatPerchSeconds}s)");
        }

        // 머무름이 끝나거나 Patrol을 떠날 때
        private void ExitPerch()
        {
            if (!_perched) return;
            _perched = false;
            _senses.ViewDistanceOverride = null;
        }
    }
}
