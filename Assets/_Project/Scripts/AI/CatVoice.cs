using RatGame.Core;
using RatGame.Data;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 고양이 상태음 (docs/07 연출 계약 "상태가 항상 소리로 먼저 들려야 한다", 고양이 242 — plan cat-242).
    /// CatVisual처럼 전 클라에서 State·SleepPhase NetworkVariable을 읽기만 한다 — 판정·동기화 없음.
    /// 의심 "냐?" · 추격 그르렁(반복) · 포획 앞발 쉭 · 깊은 잠 코골이(반복, 예고에서 멈춤). 3D 소리라 멀리·벽 너머 고양이도 방향으로 읽힌다.
    /// </summary>
    public class CatVoice : MonoBehaviour
    {
        [SerializeField] private CatBrain _brain;
        [SerializeField] private GrayboxAudioSO _audio;

        private AudioSource _oneShot, _loop;
        private CatState _lastState = (CatState)255;
        private CatSleepPhase _lastPhase = (CatSleepPhase)255;
        private float _loopVolume;
        private Vector3 _lastPos;
        private AudioSource _steps;
        private float _stepWalked;
        private float _bellTravel;
        private int _jingles; // 시험용

        private void Awake()
        {
            if (_brain == null) _brain = GetComponent<CatBrain>();
            if (_audio == null) _audio = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            _oneShot = MakeSource();
            _loop = MakeSource();
            _loop.loop = true;
            SettingsService.Changed += ApplyVolume;
        }

        private void OnDestroy() => SettingsService.Changed -= ApplyVolume;

        private AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear; // 로그 감쇠는 멀리서도 계속 들려 "어디쯤"이 흐려진다
            if (_audio != null) { s.minDistance = _audio.CatMinDistance; s.maxDistance = _audio.CatMaxDistance; }
            s.dopplerLevel = 0f;
            return s;
        }

        private static float Sfx => Mathf.Clamp01(SettingsService.Current.SfxVolume);
        private void ApplyVolume() { if (_loop != null) _loop.volume = _loopVolume * Sfx; }

        private void Update()
        {
            if (_brain == null || _audio == null || !_brain.IsSpawned) return;
            var state = _brain.State.Value;
            var phase = _brain.SleepPhase.Value;
            if (state != _lastState) OnState(state);
            if (phase != _lastPhase) OnPhase(phase);
            _lastState = state; _lastPhase = phase;
            UpdateSteps();
            UpdateBlunder(state);
            UpdateBell();
        }

        // 댕청한 실패 (고양이 273) — 종류 NV가 상태보다 늦게 올 수 있어 종류가 바뀔 때 낸다
        private CatBlunderKind _lastBlunder;
        private void UpdateBlunder(CatState state)
        {
            var kind = state == CatState.Blunder ? _brain.BlunderKind.Value : CatBlunderKind.None;
            if (kind == _lastBlunder) return;
            _lastBlunder = kind;
            var clip = _audio.BlunderClip(kind);
            if (clip != null) PlayOnce(clip, _audio.BlunderVolume);
        }

        // 발소리 (고양이 271) — 큰 고양이 화면 흔들림(CatStepShake)과 같은 걸음 폭. 흔들림은 설정으로 끄지만 소리는 정보라 효과음 볼륨만 따른다
        private void UpdateSteps()
        {
            var balance = _brain.Balance;
            if (balance == null) return;
            if (_steps == null)
            {
                _steps = gameObject.AddComponent<AudioSource>();
                _steps.playOnAwake = false; _steps.spatialBlend = 1f; _steps.rolloffMode = AudioRolloffMode.Linear;
                _steps.minDistance = 1.5f; _steps.maxDistance = _audio.CatStepMaxDistance; _steps.dopplerLevel = 0f;
            }
            Vector3 d = transform.position - _lastPos; d.y = 0f; // _lastPos는 UpdateBell이 갱신
            float step = d.magnitude;
            if (step > 3f) return; // 순간이동
            _stepWalked += step;
            float scale = _brain.BodyScale.Value;
            float stride = balance.CatStrideMeters * scale / 1.9f;
            if (_stepWalked < stride) return;
            _stepWalked = 0f;
            _steps.pitch = Mathf.Clamp(1.9f / Mathf.Max(0.5f, scale), 0.8f, 1.6f); // 작은 고양이는 가볍게
            _steps.PlayOneShot(_audio.CatStepClip, _audio.CatStepVolume(scale) * Sfx);
            _stepCount++;
        }
        private int _stepCount; // 시험용

        // 방울 단 고양이 (고양이 140·253) — 움직인 거리만큼 딸랑. 목 방울과 같은 NV(Belled)만 읽는다
        private void UpdateBell()
        {
            Vector3 pos = transform.position;
            Vector3 d = pos - _lastPos; d.y = 0f;
            _lastPos = pos;
            if (!_brain.Belled.Value || _audio.CatBellJingleVolume <= 0f) { _bellTravel = 0f; return; }
            float step = d.magnitude;
            if (step > 3f) return; // 순간이동
            _bellTravel += step;
            if (_bellTravel < _audio.CatBellStepMeters) return;
            _bellTravel = 0f;
            _oneShot.PlayOneShot(_audio.CatBellJingleClip, _audio.CatBellJingleVolume * Sfx);
            _jingles++;
        }

        private void OnState(CatState state)
        {
            if (state == CatState.Suspicious) PlayOnce(_audio.SuspiciousClip, _audio.SuspiciousVolume);
            else if (state == CatState.Capture) PlayOnce(_audio.CaptureClip, _audio.CaptureVolume);
            else if (state == CatState.Fight) PlayOnce(_audio.HissClip, _audio.HissVolume); // 앙숙 싸움 하악 (고양이 274)

            // 반복음: 추격 그르렁 / 가지고 노는 동안 가르랑(동료가 잡혀 있다는 소리, 고양이 274)
            if (state == CatState.Chase) Loop(_audio.ChaseClip, _audio.ChaseVolume);
            else if (state == CatState.Toy) Loop(_audio.PurrClip, _audio.PurrVolume);
            else if (_lastState is CatState.Chase or CatState.Toy) StopLoop();
        }

        private void OnPhase(CatSleepPhase phase)
        {
            if (phase == CatSleepPhase.Deep) Loop(_audio.SnoreClip, _audio.SnoreVolume);
            else if (_lastPhase == CatSleepPhase.Deep) StopLoop(); // ToLight = 코골이 멈춤이 곧 깬다는 예고 (자막 SnoreStop과 같은 때)
        }

        private void PlayOnce(AudioClip clip, float volume)
        {
            _oneShot.PlayOneShot(clip, volume * Sfx);
            Log.Dev($"고양이 소리: {name} {clip.name} 한 번");
        }

        private void Loop(AudioClip clip, float volume)
        {
            _loopVolume = volume;
            _loop.clip = clip;
            _loop.volume = volume * Sfx;
            _loop.time = Random.Range(0f, clip.length * 0.9f); // 두 마리가 같은 박자로 골지 않게
            _loop.Play();
            Log.Dev($"고양이 소리: {name} {clip.name} 반복");
        }

        private void StopLoop() { _loop.Stop(); _loop.clip = null; }
    }
}
