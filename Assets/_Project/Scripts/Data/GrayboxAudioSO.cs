using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 그레이박스 소리 연출값 (고양이 242, plan cat-242) — 합성음의 높이·길이·음량과 3D로 들리는 거리.
    /// 게임 판정(소음·감지)과는 무관하다 — 그건 BalanceConfigSO. 진짜 소리가 들어오면 클립 칸을 채우면 합성음 대신 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "GrayboxAudio", menuName = "RatGame/Graybox Audio")]
    public class GrayboxAudioSO : ScriptableObject
    {
        [Header("3D 거리 (m) — 이 안은 최대 크기, 밖으로 갈수록 작아져 maxDistance에서 0")]
        [SerializeField] private float _catMinDistance = 2f;
        [SerializeField] private float _catMaxDistance = 28f;

        [Header("의심 \"냐?\" — 끝이 올라가는 짧은 음")]
        [SerializeField] private AudioClip _suspiciousClip;
        [SerializeField] private float _suspiciousFrom = 520f, _suspiciousTo = 820f, _suspiciousSeconds = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _suspiciousVolume = 0.8f;

        [Header("추격 그르렁 — 추격하는 동안 반복 (주파수×길이·떨림×길이가 정수여야 이음매가 안 들린다)")]
        [SerializeField] private AudioClip _chaseClip;
        [SerializeField] private float _chaseFreq = 58f, _chaseTremolo = 22f, _chaseSeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float _chaseVolume = 0.55f;

        [Header("포획 앞발 — 짧은 쉭")]
        [SerializeField] private AudioClip _captureClip;
        [SerializeField] private float _captureSeconds = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _captureVolume = 0.9f;

        [Header("깊은 잠 코골이 — 반복, 예고(깊은 잠 → 얕은 잠)에서 멈춘다")]
        [SerializeField] private AudioClip _snoreClip;
        [SerializeField] private float _snoreSeconds = 2.6f;
        [SerializeField, Range(0f, 1f)] private float _snoreVolume = 0.45f;

        public float CatMinDistance => _catMinDistance;
        public float CatMaxDistance => _catMaxDistance;
        public float SuspiciousVolume => _suspiciousVolume;
        public float ChaseVolume => _chaseVolume;
        public float CaptureVolume => _captureVolume;
        public float SnoreVolume => _snoreVolume;

        public AudioClip SuspiciousClip => _suspiciousClip != null ? _suspiciousClip : Core.ToneSynth.Chirp(_suspiciousFrom, _suspiciousTo, _suspiciousSeconds);
        public AudioClip ChaseClip => _chaseClip != null ? _chaseClip : Core.ToneSynth.Growl(_chaseFreq, _chaseTremolo, _chaseSeconds);
        public AudioClip CaptureClip => _captureClip != null ? _captureClip : Core.ToneSynth.Swipe(_captureSeconds);
        public AudioClip SnoreClip => _snoreClip != null ? _snoreClip : Core.ToneSynth.Snore(_snoreSeconds);
    }
}
