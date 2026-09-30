using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 쥐 상태 소리 (고양이 254, plan cat-242) — 전 클라 로컬, PlayerCondition.State NV 변화만 읽는다.
    /// 덫·전선에 기절 "딱!", 끈끈이 "찰싹", 쓰러짐 "찍…", 다시 일어남 "찍!" — 동료가 어디서 곤경인지 소리로 안다. 숨기·잡혀 놀림은 다른 소리(심장·고양이)가 맡는다.
    /// </summary>
    public class RatConditionSfx : MonoBehaviour
    {
        [SerializeField] private PlayerCondition _condition;

        private GrayboxAudioSO _audio;
        private AudioSource _source;
        private ConditionState _last;
        private bool _seen;

        private void Awake()
        {
            if (_condition == null) _condition = GetComponent<PlayerCondition>();
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
            if (_condition == null || _audio == null || !_condition.IsSpawned) { _seen = false; return; }
            var now = _condition.State.Value;
            if (!_seen) { _seen = true; _last = now; return; } // 늦게 들어와 이미 쓰러진 동료에겐 소리 안 냄
            if (now == _last) return;
            AudioClip clip = now switch
            {
                ConditionState.Stunned => _audio.RatStunnedClip,
                ConditionState.Trapped => _audio.RatTrappedClip,
                ConditionState.Downed => _audio.RatDownedClip,
                ConditionState.Active when _last is ConditionState.Downed or ConditionState.Trapped or ConditionState.Pinned => _audio.RatRevivedClip,
                _ => null,
            };
            _last = now;
            if (clip == null) return;
            _source.PlayOneShot(clip, _audio.RatStateVolume * Mathf.Clamp01(SettingsService.Current.SfxVolume));
            Log.Dev($"쥐 상태 소리: {name} → {now} ({clip.name})");
        }
    }
}
