using System;
using Unity.Netcode;

namespace RatGame.Data
{
    /// <summary>업그레이드 레벨 묶음 — 저장·NetworkVariable 공용. 효과당 1바이트. (저장 JSON에 없는 필드는 0으로 읽힘)</summary>
    [Serializable]
    public struct UpgradeLevels : INetworkSerializable, IEquatable<UpgradeLevels>
    {
        public byte CarrySlots;
        public byte MoveSpeed;
        public byte StaminaMax;
        public byte ThrowPower;
        public byte JumpPower;

        public int Get(UpgradeEffect effect) => effect switch
        {
            UpgradeEffect.CarrySlots => CarrySlots,
            UpgradeEffect.MoveSpeed => MoveSpeed,
            UpgradeEffect.StaminaMax => StaminaMax,
            UpgradeEffect.ThrowPower => ThrowPower,
            UpgradeEffect.JumpPower => JumpPower,
            _ => 0
        };

        public void Set(UpgradeEffect effect, int level)
        {
            byte v = (byte)Math.Clamp(level, 0, byte.MaxValue);
            switch (effect)
            {
                case UpgradeEffect.CarrySlots: CarrySlots = v; break;
                case UpgradeEffect.MoveSpeed: MoveSpeed = v; break;
                case UpgradeEffect.StaminaMax: StaminaMax = v; break;
                case UpgradeEffect.ThrowPower: ThrowPower = v; break;
                case UpgradeEffect.JumpPower: JumpPower = v; break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CarrySlots);
            serializer.SerializeValue(ref MoveSpeed);
            serializer.SerializeValue(ref StaminaMax);
            serializer.SerializeValue(ref ThrowPower);
            serializer.SerializeValue(ref JumpPower);
        }

        public bool Equals(UpgradeLevels o) =>
            CarrySlots == o.CarrySlots && MoveSpeed == o.MoveSpeed && StaminaMax == o.StaminaMax
            && ThrowPower == o.ThrowPower && JumpPower == o.JumpPower;
        public override bool Equals(object obj) => obj is UpgradeLevels o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(CarrySlots, MoveSpeed, StaminaMax, ThrowPower, JumpPower);
        public override string ToString() => $"슬롯{CarrySlots} 속도{MoveSpeed} 스태미나{StaminaMax} 던지기{ThrowPower} 점프{JumpPower}";
    }
}
