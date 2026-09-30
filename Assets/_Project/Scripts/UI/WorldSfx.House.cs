using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 집 이벤트·고양이 루틴 예고 소리 (고양이 245, plan cat-242 4단계) — 자막(ToastWidget)과 같은 이벤트를 듣는다. 자막은 그대로 둔다(청각 접근성).
    /// 딩동·삐·딸깍·덜컹·달그락은 한 번(2D), TV·청소기·물소리는 Start~End 동안 반복(2D), 루틴 출발 방울은 고양이 자리 3D.
    /// 소리 고르기·음량은 GrayboxAudioSO(F4 소리 듣기와 같은 곳, 고양이 278).
    /// </summary>
    public partial class WorldSfx
    {
        private readonly Dictionary<HouseEventKind, AudioSource> _houseLoops = new();

        private void OnHouseEvent(HouseEventKind kind, HouseEventPhase phase)
        {
            if (_audio == null) return;
            if (phase == HouseEventPhase.Warn)
            {
                var cue = _audio.HouseWarnClip(kind);
                if (cue != null) PlayHouse(cue, $"{kind} 예고");
                if (kind == HouseEventKind.CallAway) // "나비야~" — 목소리는 설정 보이스 볼륨 (고양이 267)
                {
                    _ui.PlayOneShot(_audio.OwnerCallClip, _audio.OwnerVoiceVolume * Mathf.Clamp01(SettingsService.Current.VoiceVolume));
                    Log.Dev("집 소리: CallAway 예고 (부르는 목소리)");
                }
            }
            else if (phase == HouseEventPhase.Start)
            {
                if (kind == HouseEventKind.LightOn) PlayHouse(_audio.LightClickClip, "불 딸깍");
                var loop = _audio.HouseLoopClip(kind);
                if (loop != null) StartHouseLoop(kind, loop);
            }
            else if (_houseLoops.TryGetValue(kind, out var src) && src != null) { src.Stop(); Log.Dev($"집 소리: {kind} 반복 멈춤"); }
        }

        // 루틴 출발(밥·화장실·햇볕·잠자리·물) 방울 — 고양이 자리 3D. 코골이는 CatVoice가 소리로 낸다
        private void OnCatCue(CatCueKind kind, Vector3 pos, float hearScale)
        {
            if (_audio == null || kind == CatCueKind.Snore || kind == CatCueKind.SnoreStop) return;
            var clip = kind == CatCueKind.KittenCall ? _audio.KittenCallClip : kind == CatCueKind.Bored ? _audio.BoredClip : _audio.RoutineBellClip;
            PlayAt(pos, clip, _audio.CatBellVolume);
        }

        private void PlayHouse(AudioClip clip, string what)
        {
            _ui.PlayOneShot(clip, _audio.HouseCueVolume * Sfx);
            Log.Dev($"집 소리: {what} ({clip.name})");
        }

        private void StartHouseLoop(HouseEventKind kind, AudioClip clip)
        {
            if (!_houseLoops.TryGetValue(kind, out var src) || src == null) // 물건에 붙인 소리는 씬이 바뀌면 같이 사라진다
            {
                // TV·청소기는 그 물건에서 3D로 — 청소기가 어디 있는지(발소리가 묻히는 곳) 소리로 안다 (고양이 269). 물소리는 벽 속 전체라 2D
                Transform at = kind switch
                {
                    HouseEventKind.Vacuum => FindAnyObjectByType<World.RobotVacuum>()?.transform,
                    HouseEventKind.TV => FindAnyObjectByType<World.TvSet>()?.transform,
                    _ => null,
                };
                src = (at != null ? at.gameObject : gameObject).AddComponent<AudioSource>();
                src.playOnAwake = false; src.loop = true;
                src.spatialBlend = at != null ? 1f : 0f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = _audio.WorldMinDistance; src.maxDistance = _audio.WorldMaxDistance;
                src.dopplerLevel = 0f;
                _houseLoops[kind] = src;
            }
            src.clip = clip;
            src.volume = _audio.HouseLoopVolume * Sfx;
            src.Play();
            Log.Dev($"집 소리: {kind} 반복 ({clip.name}, {(src.spatialBlend > 0f ? "3D " + src.gameObject.name : "2D")})");
        }

        private void ApplyHouseLoopVolume()
        {
            if (_audio == null) return;
            foreach (var s in _houseLoops.Values) if (s != null) s.volume = _audio.HouseLoopVolume * Sfx;
        }

        private void StopHouseLoops() { foreach (var s in _houseLoops.Values) if (s != null) s.Stop(); }
    }
}
