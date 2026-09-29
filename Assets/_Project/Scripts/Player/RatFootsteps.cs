using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 쥐 발소리 (고양이 247, plan cat-242) — 전 클라 로컬, 위치 변화만 읽는다(판정·동기화 없음).
    /// 발걸음 소음(PlayerNoiseEmitter, 호스트)과 같은 규칙: 걷기·달리기 간격과 달리기 기준은 BalanceConfigSO, 웅크리면(스케일 50%) 소리 없음.
    /// 그래서 "내 발소리가 크게 들리면 고양이에게도 크게 들린다".
    /// </summary>
    public class RatFootsteps : MonoBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private GrayboxAudioSO _audio;

        private AudioSource _source;
        private Vector3 _lastPos;
        private float _baseScaleY, _nextStep;

        private void Awake()
        {
            if (_audio == null) _audio = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 1f;
            _source.dopplerLevel = 0f;
            if (_audio != null) _source.maxDistance = _audio.FootstepMaxDistance;
            _baseScaleY = transform.localScale.y;
            _lastPos = transform.position;
        }

        private void Update()
        {
            Vector3 pos = transform.position;
            Vector3 d = pos - _lastPos; d.y = 0f;
            _lastPos = pos;
            if (_balance == null || _audio == null || Time.deltaTime <= 0f) return;
            float raw = d.magnitude / Time.deltaTime;
            if (raw > 30f) return; // 순간이동
            _speed = Mathf.Lerp(_speed, raw, 1f - Mathf.Exp(-10f * Time.deltaTime)); // 프레임마다 튀는 값으로 걷기·달리기가 뒤바뀌지 않게
            float speed = _speed;
            bool crouching = transform.localScale.y < _baseScaleY * 0.75f;
            if (speed < 1f || Time.time < _nextStep) return;
            if (crouching) { _nextStep = Time.time + _balance.FootstepWalkInterval; _silent++; return; }
            if (!Physics.Raycast(pos, Vector3.down, transform.localScale.y + 0.35f, ~LayerMask.GetMask("Player", "Ragdoll"), QueryTriggerInteraction.Ignore)) return;

            bool running = speed > (_balance.WalkSpeed + _balance.SprintSpeed) * 0.5f;
            _nextStep = Time.time + (running ? _balance.FootstepRunInterval : _balance.FootstepWalkInterval);
            _source.pitch = Random.Range(0.9f, 1.12f); // 같은 톡이 기계처럼 반복되지 않게
            _source.PlayOneShot(_audio.FootstepClip, (running ? _audio.FootstepRunVolume : _audio.FootstepWalkVolume) * Mathf.Clamp01(SettingsService.Current.SfxVolume));
            if (running) _runs++; else _walks++;
            if (Time.time > _logUntil)
            {
                _logUntil = Time.time + 5f;
                Log.Dev($"쥐 발소리: {name} 걷기 {_walks} · 달리기 {_runs} · 웅크림 무음 {_silent} (지금 {speed:0.0}m/s)");
                _walks = _runs = _silent = 0;
            }
        }

        private float _speed, _logUntil;
        private int _walks, _runs, _silent;
    }
}
