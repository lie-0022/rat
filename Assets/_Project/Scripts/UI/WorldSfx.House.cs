using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 집 이벤트·고양이 루틴 예고 소리 (고양이 245, plan cat-242 4단계) — 자막(ToastWidget)과 같은 이벤트를 듣는다. 자막은 그대로 둔다(청각 접근성).
    /// 딩동·삐·딸깍·덜컹·달그락은 한 번(2D), TV·청소기·물소리는 Start~End 동안 반복(2D), 루틴 출발 방울은 고양이 자리 3D.
    /// 음색(높이·길이)은 그레이박스 자리값이라 여기 둔다 — 음량은 GrayboxAudioSO.
    /// </summary>
    public partial class WorldSfx
    {
        private readonly Dictionary<HouseEventKind, AudioSource> _houseLoops = new();

        private void OnHouseEvent(HouseEventKind kind, HouseEventPhase phase)
        {
            if (_audio == null) return;
            if (phase == HouseEventPhase.Warn)
            {
                var cue = kind switch
                {
                    HouseEventKind.Doorbell => ToneSynth.Chime(659f, 523f, 0.9f),   // 딩동 (내려감)
                    HouseEventKind.Vacuum => ToneSynth.Chirp(1800f, 1800f, 0.25f),  // 삐—
                    HouseEventKind.TV => ToneSynth.Clink(3000f, 0.06f),             // 리모컨 딸깍
                    HouseEventKind.Window => ToneSynth.Clink(180f, 0.35f),          // 덜컹
                    HouseEventKind.Feeding => ToneSynth.Clink(900f, 0.3f),          // 그릇 달그락
                    HouseEventKind.NewTraps => ToneSynth.Clink(2600f, 0.08f),       // 딸깍
                    HouseEventKind.Flush => ToneSynth.Growl(40f, 3f, 1f),           // 벽 속 우르릉
                    _ => null,                                                      // 부르기·불은 목소리·발소리라 음성 단계에서
                };
                if (cue != null) PlayHouse(cue, $"{kind} 예고");
            }
            else if (phase == HouseEventPhase.Start)
            {
                if (kind == HouseEventKind.LightOn) PlayHouse(ToneSynth.Clink(3000f, 0.05f), "불 딸깍");
                var loop = kind switch
                {
                    HouseEventKind.TV => ToneSynth.Babble(4f, 2f),
                    HouseEventKind.Vacuum => ToneSynth.Growl(120f, 6f, 1f),
                    HouseEventKind.Flush => ToneSynth.Rush(0.35f, 2f),
                    _ => null,
                };
                if (loop != null) StartHouseLoop(kind, loop);
            }
            else if (_houseLoops.TryGetValue(kind, out var src)) { src.Stop(); Log.Dev($"집 소리: {kind} 반복 멈춤"); }
        }

        // 루틴 출발(밥·화장실·햇볕·잠자리·물) 방울 — 고양이 자리 3D. 코골이는 CatVoice가 소리로 낸다
        private void OnCatCue(CatCueKind kind, Vector3 pos, float hearScale)
        {
            if (_audio == null || kind == CatCueKind.Snore || kind == CatCueKind.SnoreStop) return;
            var clip = kind == CatCueKind.KittenCall ? ToneSynth.Chirp(900f, 1250f, 0.3f) : ToneSynth.Clink(2600f, 0.35f);
            PlayAt(pos, clip, _audio.CatBellVolume);
        }

        private void PlayHouse(AudioClip clip, string what)
        {
            _ui.PlayOneShot(clip, _audio.HouseCueVolume * Sfx);
            Log.Dev($"집 소리: {what} ({clip.name})");
        }

        private void StartHouseLoop(HouseEventKind kind, AudioClip clip)
        {
            if (!_houseLoops.TryGetValue(kind, out var src))
            {
                src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false; src.loop = true; src.spatialBlend = 0f;
                _houseLoops[kind] = src;
            }
            src.clip = clip;
            src.volume = _audio.HouseLoopVolume * Sfx;
            src.Play();
            Log.Dev($"집 소리: {kind} 반복 ({clip.name})");
        }

        private void StopHouseLoops() { foreach (var s in _houseLoops.Values) s.Stop(); }
    }
}
