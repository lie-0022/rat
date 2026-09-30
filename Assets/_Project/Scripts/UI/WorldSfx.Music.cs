using RatGame.Core;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 추격 배경음 (고양이 248, docs/07 "Chase: 낮은 그르렁 + BGM 전환") — 전 클라 로컬, 고양이 State NV 읽기만.
    /// 어느 고양이든 Chase면 켜진다(내가 아닌 동료가 쫓겨도 — 팀 위기라서). 음량 = 음악 설정 줄 × 전체(AudioListener).
    /// </summary>
    public partial class WorldSfx
    {
        private AudioSource _music;
        private float _musicLevel, _nextCatPoll;
        private bool _anyChase;

        private void UpdateMusic()
        {
            if (_audio == null) return;
            if (Time.unscaledTime >= _nextCatPoll)
            {
                _nextCatPoll = Time.unscaledTime + 0.25f;
                bool chase = false;
                foreach (var cat in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None))
                    if (cat.IsSpawned && cat.State.Value == AI.CatState.Chase) { chase = true; break; }
                if (chase != _anyChase) Log.Dev($"추격 배경음 {(chase ? "켜짐" : "꺼짐")}");
                _anyChase = chase;
            }

            float target = _anyChase ? 1f : 0f;
            float fade = _anyChase ? _audio.ChaseMusicFadeIn : _audio.ChaseMusicFadeOut;
            _musicLevel = Mathf.MoveTowards(_musicLevel, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, fade));

            if (_musicLevel <= 0f) { if (_music != null && _music.isPlaying) _music.Stop(); return; }
            if (_music == null)
            {
                _music = gameObject.AddComponent<AudioSource>();
                _music.playOnAwake = false; _music.loop = true; _music.spatialBlend = 0f;
            }
            if (!_music.isPlaying) { _music.clip = _audio.ChaseMusicClip; _music.Play(); }
            _music.volume = _musicLevel * _audio.ChaseMusicVolume * Mathf.Clamp01(SettingsService.Current.MusicVolume);
        }
    }
}
