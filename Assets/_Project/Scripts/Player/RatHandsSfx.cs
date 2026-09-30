using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 잡기 소리 (고양이 255, plan cat-242) — 전 클라 로컬, CarriedItemNetId NV가 새 물건으로 바뀌면 그 쥐 자리에서 "톡".
    /// 던지기 "휙"은 호스트가 보내는 ThrownClientRpc → EventBus.ItemThrown → WorldSfx (위치 변화로는 던짐·내려놓기를 못 갈랐다).
    /// </summary>
    public class RatHandsSfx : MonoBehaviour
    {
        [SerializeField] private PlayerCarryController _carry;

        private GrayboxAudioSO _audio;
        private AudioSource _source;
        private ulong _lastHeld;
        private bool _seen;

        private void Awake()
        {
            if (_carry == null) _carry = GetComponent<PlayerCarryController>();
            _audio = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.dopplerLevel = 0f;
            if (_audio != null) { _source.minDistance = _audio.WorldMinDistance; _source.maxDistance = _audio.WorldMaxDistance; }
        }

        private void Update()
        {
            if (_carry == null || _audio == null || !_carry.IsSpawned) { _seen = false; return; }
            ulong held = _carry.CarriedItemNetId.Value;
            if (!_seen) { _seen = true; _lastHeld = held; return; }
            if (held == _lastHeld) return;
            if (held != 0) _source.PlayOneShot(_audio.GrabClip, _audio.GrabVolume * Mathf.Clamp01(SettingsService.Current.SfxVolume));
            _lastHeld = held;
        }
    }
}
