using RatGame.Core;
using RatGame.Data;
using RatGame.Noise;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// TV (design/cat-ideas/10, 2026-09-24) — 집주인 이벤트 "TV"의 몸체. 켜지면 90s 동안 배경 소리(25 미만 묻힘)를 내고
    /// 고양이가 앞에 앉아 화면만 본다(CatBrain.ServerWatchTv). 화면 깜빡임은 On NV로 전 클라.
    /// </summary>
    public class TvSet : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public NetworkVariable<bool> On = new NetworkVariable<bool>(false);

        private float _until;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        /// <summary>고양이가 앉을 곳 — 화면 앞(transform.forward) tvWatchDistance.</summary>
        public Vector3 WatchPoint
        {
            get
            {
                Vector3 f = transform.forward; f.y = 0f;
                Vector3 p = transform.position + f.normalized * (_balance != null ? _balance.TvWatchDistance : 2.5f);
                p.y = 0f;
                return p;
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            On.OnValueChanged += (_, on) => Log.Dev($"TV 연출: {(on ? "켜짐" : "꺼짐")}"); // 2인 검증용
        }

        /// <summary>호스트: TV 켜기.</summary>
        public void ServerStart(float seconds)
        {
            if (!IsServer) return;
            _until = Time.time + seconds;
            On.Value = true;
            NoiseSystem.SetMask(this, _balance.TvMaskLoudness);
            Log.Dev($"TV: 켜짐 ({seconds}s, 소리 {_balance.TvMaskLoudness} 미만 묻힘)");
        }

        private void Update()
        {
            if (_renderer != null)
            {
                // 켜져 있으면 화면이 푸르스름하게 깜빡인다
                Color c = On.Value ? Color.Lerp(new Color(0.3f, 0.45f, 0.8f), new Color(0.7f, 0.8f, 1f), Mathf.PerlinNoise(Time.time * 4f, 0f)) : new Color(0.08f, 0.08f, 0.1f);
                _block.SetColor("_BaseColor", c);
                _renderer.SetPropertyBlock(_block);
            }
            if (!IsServer || !On.Value || Time.time < _until) return;
            On.Value = false;
            NoiseSystem.SetMask(this, 0f);
            if (Run.RunManager.Instance != null) Run.RunManager.Instance.ServerHouseEvent(HouseEventKind.TV, HouseEventPhase.End);
            Log.Dev("TV: 꺼짐");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NoiseSystem.SetMask(this, 0f);
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
