using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 큰 고양이 발걸음 (고양이 145, plan cat-145): 가까운 큰 고양이(몸 배율 &gt; 1 — 벽 속)가 한 걸음 옮길 때마다 내 시점이 살짝 "쿵" 내려앉는다.
    /// 소유 클라 화면만(PlayerCameraRig가 붙이고 Offset을 읽음) — 고양이 위치·BodyScale은 이미 모두에게 있어 새 동기화 없음. 설정 "화면 흔들림"으로 끈다.
    /// </summary>
    public class CatStepShake : MonoBehaviour
    {
        /// <summary>이번 프레임 시점 높이 보정 (m, 0 이하).</summary>
        public float Offset { get; private set; }

        private BalanceConfigSO _balance;
        private CatBrain[] _cats = new CatBrain[0];
        private readonly Dictionary<CatBrain, (Vector3 last, float walked)> _steps = new();
        private float _impulse;
        private float _nextScan;
        private int _thumps;

        private void Start() => _balance = GetComponent<PlayerController>()?.Balance;

        private void LateUpdate()
        {
            _impulse *= Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, _balance != null ? _balance.CatStepShakeDecaySeconds : 0.12f));
            if (_balance == null || !SettingsService.Current.CameraShake) { Offset = 0f; return; }
            if (Time.time >= _nextScan) { _nextScan = Time.time + 1f; _cats = FindObjectsByType<CatBrain>(FindObjectsSortMode.None); }

            float radius = _balance.CatStepShakeRadius;
            foreach (var cat in _cats)
            {
                if (cat == null || !cat.IsSpawned || cat.BodyScale.Value <= 1.01f) continue;
                Vector3 p = cat.transform.position;
                if (!_steps.TryGetValue(cat, out var st)) { _steps[cat] = (p, 0f); continue; }
                float walked = st.walked + Vector3.Distance(p, st.last);
                float stride = _balance.CatStrideMeters * cat.BodyScale.Value / 1.9f; // 걸음 폭은 몸에 비례 (1.9 = 벽 속 기본 배율)
                float d = Vector3.Distance(p, transform.position);
                if (walked >= stride)
                {
                    walked = 0f;
                    if (d < radius)
                    {
                        float k = 1f - d / radius;
                        _impulse = Mathf.Max(_impulse, _balance.CatStepShakeAmplitude * k * k);
                        if (_thumps++ % 20 == 0) Log.Dev($"발걸음 흔들림 연출: {d:F1}m, {_impulse:F3}"); // 2인 검증용 — 20걸음에 한 번
                    }
                }
                _steps[cat] = (p, walked);
            }
            Offset = -_impulse;
        }
    }
}
