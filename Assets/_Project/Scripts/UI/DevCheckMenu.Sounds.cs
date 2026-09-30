using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// F4 "소리 듣기" (고양이 277) — 그레이박스 합성음을 상황을 기다리지 않고 바로 들어 보기(체크리스트 U-242~276 확인용).
    /// 내 화면에서만 2D로. 진짜 소리를 `Resources/GrayboxAudio` 칸에 넣으면 여기서도 그 소리가 난다.
    /// </summary>
    public partial class DevCheckMenu
    {
        private bool _soundsOpen;
        private AudioSource _preview;

        private void DrawSounds()
        {
            GUILayout.Space(6);
            if (GUILayout.Button(_soundsOpen ? "▼ 소리 듣기 (합성음)" : "▶ 소리 듣기 (합성음)")) _soundsOpen = !_soundsOpen;
            if (!_soundsOpen) return;
            var a = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            if (a == null) { GUILayout.Label("GrayboxAudio 없음"); return; }
            if (_preview == null) { _preview = gameObject.AddComponent<AudioSource>(); _preview.playOnAwake = false; _preview.spatialBlend = 0f; }
            Row("고양이", new List<(string, AudioClip)>
            {
                ("냐?", a.SuspiciousClip), ("그르렁", a.ChaseClip), ("쉭", a.CaptureClip), ("코골이", a.SnoreClip),
                ("방울", a.CatBellJingleClip), ("발소리", a.CatStepClip), ("하악", a.HissClip), ("가르랑", a.PurrClip),
                ("에취", a.BlunderClip(CatBlunderKind.Sneeze)), ("냥!", a.BlunderClip(CatBlunderKind.Startle)), ("주르륵", a.BlunderClip(CatBlunderKind.Slip)), ("웩웩", a.BlunderClip(CatBlunderKind.Hairball)),
            });
            Row("쥐·물건", new List<(string, AudioClip)>
            {
                ("발소리", a.FootstepClip), ("찍찍", a.SqueakClip), ("잡기 톡", a.GrabClip), ("던지기 휙", a.ThrowClip),
                ("덫 딱", a.RatStunnedClip), ("끈끈이", a.RatTrappedClip), ("쓰러짐", a.RatDownedClip), ("살아남", a.RatRevivedClip),
                ("쨍", a.ImpactClip), ("정산 딸랑", a.DepositClip),
            });
            Row("집", new List<(string, AudioClip)>
            {
                ("딩동", ToneSynth.Chime(659f, 523f, 0.9f)), ("청소기 삐", ToneSynth.Chirp(1800f, 1800f, 0.25f)), ("청소기", ToneSynth.Growl(120f, 6f, 1f)),
                ("TV", ToneSynth.Babble(4f, 2f)), ("덜컹", ToneSynth.Clink(180f, 0.35f)), ("달그락", ToneSynth.Clink(900f, 0.3f)),
                ("우르릉", ToneSynth.Growl(40f, 3f, 1f)), ("쏴아", ToneSynth.Rush(0.35f, 2f)), ("나~비야", ToneSynth.CallOut(1.1f)),
                ("쿵쿵쿵", ToneSynth.HeavySteps(1.1f)), ("전기선", a.WireHumClip), ("루틴 방울", ToneSynth.Clink(2600f, 0.35f)),
            });
            Row("알림·음악", new List<(string, AudioClip)>
            {
                ("할당량", a.QuotaClip), ("도전과제", a.AchievementClip), ("도감", a.CodexClip), ("오독오독", a.EatClip),
                ("삑", a.CountdownTick), ("삐익", a.CountdownGo), ("UI 톡", a.UiClickClip), ("심장", a.HeartbeatClip), ("추격 음악", a.ChaseMusicClip),
            });
            if (GUILayout.Button("멈춤")) _preview.Stop();
        }

        private void Row(string title, List<(string label, AudioClip clip)> items)
        {
            GUILayout.Label($"<b>{title}</b>", Rich());
            for (int i = 0; i < items.Count; i += 4)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 4, items.Count); j++)
                    if (GUILayout.Button(items[j].label) && items[j].clip != null)
                    {
                        _preview.Stop();
                        _preview.clip = items[j].clip;
                        _preview.volume = Mathf.Clamp01(SettingsService.Current.SfxVolume);
                        _preview.Play();
                        Log.Dev($"소리 듣기: {items[j].label} ({items[j].clip.name})");
                    }
                GUILayout.EndHorizontal();
            }
        }
    }
}
