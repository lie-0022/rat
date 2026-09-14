using RatGame.Data;
using RatGame.Meta;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 이 플레이어에게 적용 중인 팀 업그레이드 (docs/11 상점). 호스트가 스폰 시·구매 시 쓰고 전 클라가 읽는다.
    /// 효과 배율은 BalanceConfigSO 수치 × 레벨 — 이동·스태미나·던지기는 소유 클라가, 슬롯 수는 서버가 반영.
    /// </summary>
    public class PlayerUpgrades : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<UpgradeLevels> Levels = new();

        public float MoveSpeedMultiplier => 1f + Levels.Value.MoveSpeed * _balance.UpgradeMoveSpeedPerLevel;
        public float StaminaMaxMultiplier => 1f + Levels.Value.StaminaMax * _balance.UpgradeStaminaPerLevel;
        public float ThrowPowerMultiplier => 1f + Levels.Value.ThrowPower * _balance.UpgradeThrowPerLevel;
        public float JumpPowerMultiplier => 1f + Levels.Value.JumpPower * _balance.UpgradeJumpPerLevel;
        public int CarrySlotCount => _balance.BaseCarrySlots + Levels.Value.CarrySlots;

        public override void OnNetworkSpawn()
        {
            if (IsServer) ServerApply(MetaUpgrades.Levels);
        }

        /// <summary>호스트: 레벨을 쓰고 슬롯 수를 맞춘다 (슬롯은 늘기만 한다 — 든 물건이 사라지지 않게).</summary>
        public void ServerApply(UpgradeLevels levels)
        {
            if (!IsServer) return;
            Levels.Value = levels;
            var carry = GetComponent<PlayerCarryController>();
            if (carry != null) carry.ServerEnsureSlots(CarrySlotCount);
        }

        /// <summary>호스트: 접속 중인 전원에게 같은 레벨 적용 (구매 직후).</summary>
        public static void ServerApplyToAll(UpgradeLevels levels)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var up = client.PlayerObject.GetComponent<PlayerUpgrades>();
                if (up != null) up.ServerApply(levels);
            }
        }
    }
}
