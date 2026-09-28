using System;
using Unity.Netcode;

namespace RatGame.Run
{
    /// <summary>
    /// 결과 화면 "오늘의 쥐들" 한 줄 (docs/09·12). 호스트가 RunManager.Contributions에 쓰고 클라 HUD가 읽는다
    /// (EventBus.RunEnded는 호스트 로컬이라 클라가 못 받음).
    /// </summary>
    public struct PlayerContribution : INetworkSerializable, IEquatable<PlayerContribution>
    {
        public ulong ClientId;
        /// <summary>이 쥐가 마지막으로 놓아 쥐구멍에 들어간 가치 합.</summary>
        public int DepositedValue;
        public int DepositCount;
        /// <summary>귀환 순간 손·주머니에 든 가치 (여럿이 든 대형은 먼저 센 한 명에게).</summary>
        public int CarriedValue;
        /// <summary>결과 시점에 다운 상태였나.</summary>
        public bool Downed;
        /// <summary>동료 구조 횟수 — 끈끈이 구출·쓰러진 몸 부활 (고양이 186).</summary>
        public int Rescues;

        public int Total => DepositedValue + CarriedValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref DepositedValue);
            serializer.SerializeValue(ref DepositCount);
            serializer.SerializeValue(ref CarriedValue);
            serializer.SerializeValue(ref Downed);
            serializer.SerializeValue(ref Rescues);
        }

        public bool Equals(PlayerContribution other) =>
            ClientId == other.ClientId && DepositedValue == other.DepositedValue && DepositCount == other.DepositCount
            && CarriedValue == other.CarriedValue && Downed == other.Downed && Rescues == other.Rescues;
    }
}
