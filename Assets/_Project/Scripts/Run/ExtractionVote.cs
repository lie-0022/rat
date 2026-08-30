using System;
using RatGame.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Run
{
    public struct VoteEntry : INetworkSerializable, IEquatable<VoteEntry>
    {
        public ulong ClientId;
        public bool GoDeeper;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref GoDeeper);
        }

        public bool Equals(VoteEntry other) => ClientId == other.ClientId && GoDeeper == other.GoDeeper;
    }

    /// <summary>
    /// 탈출 투표 (docs/09). 30s 제한, 미투표=나간다, 과반 "더 간다"만 진행 (2인 1:1 → 나간다).
    /// HUD 실시간 집계는 UI 태스크(2-6) — Votes NetworkList가 그 데이터원.
    /// </summary>
    public class ExtractionVote : NetworkBehaviour
    {
        public NetworkList<VoteEntry> Votes = new NetworkList<VoteEntry>();
        public NetworkVariable<double> VoteDeadline = new(0);

        private const float VoteSeconds = 30f;
        private bool _open;

        public void ServerOpenVote()
        {
            if (!IsServer) return;
            Votes.Clear();
            VoteDeadline.Value = NetworkManager.ServerTime.Time + VoteSeconds;
            _open = true;
            Log.Dev($"탈출 투표 시작 ({VoteSeconds}s) — 더 간다/나간다");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CastVoteServerRpc(bool goDeeper, ServerRpcParams rpcParams = default)
        {
            if (!_open) return;
            ulong clientId = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < Votes.Count; i++)
                if (Votes[i].ClientId == clientId) { Votes.RemoveAt(i); break; }
            Votes.Add(new VoteEntry { ClientId = clientId, GoDeeper = goDeeper });
            Log.Dev($"투표: client {clientId} → {(goDeeper ? "더 간다" : "나간다")} ({Votes.Count}/{NetworkManager.ConnectedClientsIds.Count})");

            if (Votes.Count >= NetworkManager.ConnectedClientsIds.Count) Resolve();
        }

        private void Update()
        {
            if (!IsServer || !_open) return;
            if (NetworkManager.ServerTime.Time >= VoteDeadline.Value) Resolve();
        }

        private void Resolve()
        {
            _open = false;
            int total = NetworkManager.ConnectedClientsIds.Count;
            int deeper = 0;
            foreach (var vote in Votes) if (vote.GoDeeper) deeper++;
            bool goDeeper = deeper * 2 > total; // 과반 — 미투표는 "나간다"로 계산됨 (docs/09)
            Log.Dev($"투표 결과: 더 간다 {deeper}/{total} → {(goDeeper ? "진행" : "탈출")}");
            GetComponent<RunManager>().ServerOnVoteResult(goDeeper);
        }
    }
}
