using System.Collections.Generic;
using RatGame.Core;
using RatGame.Player;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 끈끈이 (docs/10 Trap_GluePad, docs/04 Trapped — 2026-09-24 고양이 49). 밟으면 Trapped — 동료가 E 1.5s로 구출(PlayerCondition).
    /// 구출된 쥐는 잠깐 같은 끈끈이에 다시 안 붙는다(발 떼고 나갈 시간). 계속 쓰인다.
    /// </summary>
    public class GluePad : TrapBase
    {
        private readonly Dictionary<ulong, float> _graceUntil = new();
        // 쥐 오브젝트는 씬을 넘어 살아 있어서, 구독을 안 풀면 사라진 끈끈이의 처리기가 다음 스테이지에서 불려 예외가 났다 (고양이 125)
        private readonly List<(PlayerCondition cond, Unity.Netcode.NetworkVariable<ConditionState>.OnValueChangedDelegate handler)> _watched = new();
        private System.Action<ulong> _onConnected;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            // 이 끈끈이에서 풀려난 쥐 — 유예 시작
            foreach (var client in NetworkManager.ConnectedClientsList) Watch(client.PlayerObject);
            _onConnected = id =>
            {
                if (NetworkManager.ConnectedClients.TryGetValue(id, out var c)) Watch(c.PlayerObject);
            };
            NetworkManager.OnClientConnectedCallback += _onConnected;
        }

        public override void OnNetworkDespawn()
        {
            foreach (var (cond, handler) in _watched) if (cond != null) cond.State.OnValueChanged -= handler;
            _watched.Clear();
            if (_onConnected != null && NetworkManager != null) NetworkManager.OnClientConnectedCallback -= _onConnected;
            _onConnected = null;
            base.OnNetworkDespawn();
        }

        private void Watch(Unity.Netcode.NetworkObject po)
        {
            var cond = po != null ? po.GetComponent<PlayerCondition>() : null;
            if (cond == null) return;
            ulong id = cond.OwnerClientId;
            Unity.Netcode.NetworkVariable<ConditionState>.OnValueChangedDelegate handler = (prev, next) =>
            {
                if (this != null && prev == ConditionState.Trapped && next == ConditionState.Active && Contains(cond.transform.position, 1.2f))
                    _graceUntil[id] = Time.time + _balance.GlueGraceSeconds;
            };
            cond.State.OnValueChanged += handler;
            _watched.Add((cond, handler));
        }

        protected override void OnRat(PlayerCondition rat)
        {
            if (_graceUntil.TryGetValue(rat.OwnerClientId, out float until) && Time.time < until) return;
            rat.ServerSetState(ConditionState.Trapped);
            Log.Dev($"끈끈이: client {rat.OwnerClientId} 붙음");
        }
    }
}
