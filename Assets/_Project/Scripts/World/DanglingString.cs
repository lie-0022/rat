using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 흔들리는 끈 (design/cat-ideas/13, 2026-09-24 고양이 38). 매달린 장난감 — 쥐가 툭 치거나(E) 옆을 지나가면 30s 흔들리고,
    /// 그동안 시야 안의 한가한 고양이가 와서 앞발질한다(Distracted). 같은 고양이는 한동안 다시 안 속는다.
    /// 판정은 호스트, 흔들림 연출은 Swinging NV로 전 클라.
    /// </summary>
    public class DanglingString : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private Transform _swingPivot; // 끈 윗끝 — 이걸 흔든다

        public NetworkVariable<bool> Swinging = new NetworkVariable<bool>(false);

        private float _swingUntil;
        private float _nextCheck;
        private readonly Dictionary<CatBrain, float> _catCooldown = new();

        /// <summary>고양이가 와서 노는 곳 (바닥).</summary>
        public Vector3 PlayPoint { get { var p = transform.position; p.y = 0f; return p; } }

        public string PromptText => Loc.T("끈 흔들기");
        public float HoldSeconds => 0.3f;
        public bool CanInteract(ulong clientId) => !Swinging.Value;

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer) return;
            StartSwing($"client {clientId}가 툭");
        }

        private void StartSwing(string why)
        {
            _swingUntil = Time.time + _balance.StringSwingSeconds;
            if (!Swinging.Value) Swinging.Value = true;
            Log.Dev($"끈: 흔들림 — {why} ({_balance.StringSwingSeconds}s)");
        }

        public override void OnNetworkSpawn()
        {
            Swinging.OnValueChanged += (_, on) => Log.Dev($"끈 연출: {(on ? "흔들림" : "멈춤")}"); // 2인 검증용
        }

        private void Update()
        {
            // 연출 (모든 클라): 흔들리면 진자처럼
            if (_swingPivot != null)
                _swingPivot.localRotation = Swinging.Value ? Quaternion.Euler(Mathf.Sin(Time.time * 5f) * 35f, 0f, Mathf.Sin(Time.time * 3.1f) * 15f) : Quaternion.identity;

            if (!IsServer || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;
            if (Swinging.Value && Time.time >= _swingUntil) { Swinging.Value = false; Log.Dev("끈: 멈춤"); }
            if (!Swinging.Value) { CheckRetrigger(); return; }
            AttractCats();
        }

        // 멈춘 끈 옆을 쥐가 지나가면 다시 흔들린다
        private void CheckRetrigger()
        {
            float r2 = _balance.StringRetriggerRadius * _balance.StringRetriggerRadius;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var po = client.PlayerObject;
                if (po == null) continue;
                var cond = po.GetComponent<PlayerCondition>();
                if (cond == null || cond.State.Value != ConditionState.Active) continue;
                Vector3 d = po.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                StartSwing($"client {client.ClientId}가 스침");
                return;
            }
        }

        private void AttractCats()
        {
            int blockMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker", "Default");
            foreach (var cat in FindObjectsByType<CatBrain>(FindObjectsSortMode.None))
            {
                if (cat == null || !cat.IsSpawned) continue;
                var st = cat.State.Value;
                if (st != CatState.Patrol && st != CatState.Return && st != CatState.Search) continue;
                if (_catCooldown.TryGetValue(cat, out float until) && Time.time < until) continue;
                if (Vector3.Distance(cat.transform.position, PlayPoint) > _balance.StringAttractRadius) continue;
                Vector3 eye = cat.transform.position + Vector3.up * 0.5f;
                Vector3 toy = _swingPivot != null ? _swingPivot.position + Vector3.down * 0.5f : transform.position;
                // 끈 자기 기둥에 맞는 건 보이는 것 (기둥 바로 뒤에서 보면 기둥이 선을 막는다)
                if (Physics.Linecast(eye, toy, out var hit, blockMask, QueryTriggerInteraction.Ignore) && hit.collider.gameObject != gameObject) continue;
                _catCooldown[cat] = Time.time + _balance.StringCatCooldown;
                cat.ServerDistract(PlayPoint, _balance.StringDistractSeconds, AttentionKind.String);
                Log.Dev($"끈: 고양이 [{cat.name}] 앞발질하러 옴");
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance, Transform pivot) { _balance = balance; _swingPivot = pivot; }
#endif
    }
}
