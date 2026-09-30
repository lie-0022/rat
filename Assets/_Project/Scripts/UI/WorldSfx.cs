using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 쥐·물건 그레이박스 소리 (고양이 244, plan cat-242) — 전 클라 로컬, 구독·읽기만.
    /// 찍찍(RatSqueak) · 큰 소음 파문(NoiseRipple — 깨짐·세게 떨어뜨림, loudness ≥ rippleThreshold)은 그 자리 3D,
    /// 정산 "딸랑"은 쥐구멍 적립(StashedValue NV)이 오를 때 2D. 정산·깨짐 이벤트(LootDeposited·LootBroken)는 호스트 전용이라 쓰지 않는다.
    /// 정적 Instance 없음 — AchievementService처럼 스스로 생겨 씬을 넘어 산다.
    /// </summary>
    public partial class WorldSfx : MonoBehaviour
    {
        private const int PoolSize = 6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("WorldSfx");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldSfx>();
        }

        private GrayboxAudioSO _audio;
        private readonly AudioSource[] _pool = new AudioSource[PoolSize];
        private int _next;
        private AudioSource _ui;
        private Run.RunManager _run;
        private int _lastStash;

        private void Awake()
        {
            _audio = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Sfx" + i);
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.dopplerLevel = 0f;
                if (_audio != null) { s.minDistance = _audio.WorldMinDistance; s.maxDistance = _audio.WorldMaxDistance; }
                _pool[i] = s;
            }
            _ui = gameObject.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;
            EventBus.RatSqueak += OnSqueak;
            EventBus.NoiseRipple += OnRipple;
            EventBus.HouseEvent += OnHouseEvent;
            EventBus.CatCue += OnCatCue;
            EventBus.ItemThrown += OnThrown;
            SettingsService.Changed += ApplyHouseLoopVolume; // 켜져 있는 반복음도 효과음 줄을 바로 따른다 (고양이 251)
        }

        private void OnDestroy()
        {
            EventBus.RatSqueak -= OnSqueak;
            EventBus.NoiseRipple -= OnRipple;
            EventBus.HouseEvent -= OnHouseEvent;
            EventBus.CatCue -= OnCatCue;
            EventBus.ItemThrown -= OnThrown;
            SettingsService.Changed -= ApplyHouseLoopVolume;
        }

        private static float Sfx => Mathf.Clamp01(SettingsService.Current.SfxVolume);

        private void OnSqueak(ulong owner, Vector3 pos) { if (_audio != null) PlayAt(pos, _audio.SqueakClip, _audio.SqueakVolume); }
        private void OnThrown(Vector3 pos) { if (_audio != null) PlayAt(pos, _audio.ThrowClip, _audio.ThrowVolume); }
        private void OnRipple(Vector3 pos, float loudness) { if (_audio != null) PlayAt(pos, _audio.ImpactClip, _audio.ImpactVolume(loudness)); }

        // 돌아가며 쓰는 소리 자리 — 여섯 개가 동시에 울리면 가장 오래된 것부터 끊긴다
        private void PlayAt(Vector3 pos, AudioClip clip, float volume)
        {
            var s = _pool[_next];
            _next = (_next + 1) % PoolSize;
            s.transform.position = pos;
            s.PlayOneShot(clip, volume * Sfx);
            Log.Dev($"쥐·물건 소리: {clip.name} ({pos.x:0.0}, {pos.z:0.0})");
        }

        // 정산 딸랑 — 적립이 오른 순간. 스테이지가 바뀌어 0이 되거나 RunManager가 바뀌면 기준만 다시 잡는다
        private void Update()
        {
            UpdateMusic();
            var run = Run.RunManager.Instance;
            if (run == null || !run.IsSpawned) { if (_run != null) StopHouseLoops(); _run = null; return; } // 런이 끝나면 켜져 있던 TV·청소기 소리도 끈다
            int stash = run.StashedValue.Value;
            if (run != _run) { _run = run; _lastStash = stash; return; }
            if (stash > _lastStash && _audio != null)
            {
                _ui.PlayOneShot(_audio.DepositClip, _audio.DepositVolume * Sfx);
                Log.Dev($"쥐·물건 소리: 정산 딸랑 (+{stash - _lastStash})");
            }
            _lastStash = stash;
        }
    }
}
