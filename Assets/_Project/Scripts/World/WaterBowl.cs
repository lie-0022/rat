using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 물그릇 (design/cat-ideas/06 2단계, 2026-09-24). 쥐가 E 1s로 엎으면 60s 웅덩이 — 양날의 도구.
    ///  - 웅덩이를 지난 쥐: 20s 젖은 발자국(냄새 강도 40 — 치즈 없이도 고양이가 따라온다)
    ///  - 웅덩이 안 냄새 자국은 지워진다(치즈 자국 끊기)
    ///  - 달리던 고양이는 웅덩이에서 미끄러진다(CatBrain.CheckSlip이 ActivePuddles를 본다)
    /// 판정은 호스트, 웅덩이 표시는 Spilled NV로 전 클라.
    /// </summary>
    public class WaterBowl : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private Transform _puddleVisual; // 파란 원판 (없으면 런타임 생성)

        public NetworkVariable<bool> Spilled = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> PuddleActive = new NetworkVariable<bool>(false);

        /// <summary>호스트: 지금 활성인 웅덩이들 (고양이 미끄러짐 판정용).</summary>
        public static readonly List<WaterBowl> ActivePuddles = new();

        private float _puddleUntil;

        public Vector3 PuddleCenter => transform.position;
        public float PuddleRadius => _balance != null ? _balance.PuddleRadius : 2.5f;

        public string PromptText => Loc.T("물그릇 엎기");
        public float HoldSeconds => 1f;
        public bool CanInteract(ulong clientId) => !Spilled.Value;

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer || Spilled.Value) return;
            Spilled.Value = true;
            PuddleActive.Value = true;
            _puddleUntil = Time.time + _balance.PuddleSeconds;
            if (!ActivePuddles.Contains(this)) ActivePuddles.Add(this);
            NoiseSystem.Emit(transform.position, _balance.WaterBowlSpillNoise, NoiseType.Impact, clientId);
            Log.Dev($"물그릇: client {clientId}가 엎음 — 웅덩이 {_balance.PuddleSeconds}s");
        }

        public override void OnNetworkSpawn()
        {
            PuddleActive.OnValueChanged += (_, on) => ShowPuddle(on);
            ShowPuddle(PuddleActive.Value);
        }

        public override void OnNetworkDespawn() => ActivePuddles.Remove(this);

        public bool Contains(Vector3 pos)
        {
            Vector3 d = pos - PuddleCenter; d.y = 0f;
            return d.sqrMagnitude <= PuddleRadius * PuddleRadius;
        }

        private void Update()
        {
            if (!IsServer || !PuddleActive.Value) return;
            if (Time.time >= _puddleUntil)
            {
                PuddleActive.Value = false;
                ActivePuddles.Remove(this);
                Log.Dev("물그릇: 웅덩이 마름");
                return;
            }
            // 안에 있는 쥐는 젖는다
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var po = client.PlayerObject;
                if (po == null || !Contains(po.transform.position)) continue;
                var scent = po.GetComponent<PlayerScent>();
                if (scent != null) scent.ServerMarkWet(_balance.WetSeconds);
            }
            // 안의 냄새 자국은 씻겨 나간다
            ScentSystem.EraseInRadius(PuddleCenter, PuddleRadius);
        }

        private void ShowPuddle(bool on)
        {
            if (_puddleVisual == null && on)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Puddle";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                float r = PuddleRadius * 2f / Mathf.Max(0.01f, transform.lossyScale.x);
                go.transform.localScale = new Vector3(r, 0.01f / Mathf.Max(0.01f, transform.lossyScale.y), r);
                go.transform.position = new Vector3(transform.position.x, 0.02f, transform.position.z);
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.3f, 0.55f, 0.9f));
                go.GetComponent<Renderer>().SetPropertyBlock(block);
                _puddleVisual = go.transform;
            }
            if (_puddleVisual != null) _puddleVisual.gameObject.SetActive(on);
            Log.Dev($"웅덩이 연출: {(on ? "보임" : "마름")}"); // 2인 검증용
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
