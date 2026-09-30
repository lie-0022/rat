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

        [Header("쥐·물건 (고양이 244) — 찍찍·큰 소음 파문(깨짐 포함)은 그 자리 3D, 정산 딸랑은 2D")]
        [SerializeField] private float _worldMinDistance = 1.5f;
        [SerializeField] private float _worldMaxDistance = 30f;
        [SerializeField] private AudioClip _squeakClip;
        [SerializeField] private float _squeakFreq = 2400f, _squeakSeconds = 0.16f;
        [SerializeField, Range(0f, 1f)] private float _squeakVolume = 0.6f;
        [SerializeField] private AudioClip _impactClip;
        [SerializeField] private float _impactFreq = 1150f, _impactSeconds = 0.5f;
        [Tooltip("소음 크기(loudness) 40 → 이 음량의 절반, 80 이상 → 이 음량")]
        [SerializeField, Range(0f, 1f)] private float _impactVolume = 0.8f;
        [SerializeField] private AudioClip _depositClip;
        [SerializeField] private float _depositFrom = 1046f, _depositTo = 1568f, _depositSeconds = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _depositVolume = 0.5f;

        [Header("집 이벤트 (고양이 245) — 집 전체에서 들리는 소리라 2D. 루틴 예고 방울만 고양이 자리 3D")]
        [SerializeField, Range(0f, 1f)] private float _houseCueVolume = 0.55f;   // 딩동·삐·딸깍·덜컹
        [SerializeField, Range(0f, 1f)] private float _houseLoopVolume = 0.25f;  // TV·청소기·물소리 — 켜져 있는 동안 반복
        [SerializeField, Range(0f, 1f)] private float _catBellVolume = 0.6f;     // 루틴 출발 방울

        [Header("쥐 발소리 (고양이 247) — 간격·달리기 기준은 BalanceConfigSO 발걸음 값(소음과 같은 박자). 웅크리면 소리 없음")]
        [SerializeField] private AudioClip _footstepClip;
        [SerializeField] private float _footstepBody = 140f, _footstepSeconds = 0.09f;
        [SerializeField, Range(0f, 1f)] private float _footstepWalkVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _footstepRunVolume = 0.35f;
        [SerializeField] private float _footstepMaxDistance = 18f;

        public AudioClip FootstepClip => _footstepClip != null ? _footstepClip : Core.ToneSynth.Tap(_footstepBody, _footstepSeconds);
        public float FootstepWalkVolume => _footstepWalkVolume;
        public float FootstepRunVolume => _footstepRunVolume;
        public float FootstepMaxDistance => _footstepMaxDistance;

        [Header("추격 배경음 (고양이 248) — 어느 고양이든 추격 중이면 서서히 켜짐, 끝나면 서서히 꺼짐. 음량 = 음악 설정 줄")]
        [SerializeField] private AudioClip _chaseMusicClip;
        [SerializeField] private float _chaseMusicBpm = 120f, _chaseMusicRoot = 73.4f;
        [SerializeField, Range(0f, 1f)] private float _chaseMusicVolume = 0.45f;
        [SerializeField] private float _chaseMusicFadeIn = 0.6f, _chaseMusicFadeOut = 2.5f; // 초 — 켜짐은 빨리(위험 신호), 꺼짐은 천천히(안도)

        public AudioClip ChaseMusicClip => _chaseMusicClip != null ? _chaseMusicClip : Core.ToneSynth.ChaseMusic(_chaseMusicBpm, _chaseMusicRoot);
        public float ChaseMusicVolume => _chaseMusicVolume;
        public float ChaseMusicFadeIn => _chaseMusicFadeIn;
        public float ChaseMusicFadeOut => _chaseMusicFadeOut;

        [Header("숨을 곳 심장 소리 (고양이 252) — 화면 펄스와 같은 박자, 고양이가 멀면 이 음량의 절반·코앞이면 전부")]
        [SerializeField] private AudioClip _heartbeatClip;
        [SerializeField, Range(0f, 1f)] private float _heartbeatVolume = 0.7f;

        public AudioClip HeartbeatClip => _heartbeatClip != null ? _heartbeatClip : Core.ToneSynth.Heartbeat(0.45f);
        public float HeartbeatVolume(float closeness) => _heartbeatVolume * Mathf.Lerp(0.5f, 1f, closeness);

        [Header("고양이 방울 (고양이 253) — 방울 단 고양이가 움직이면 걸음마다 딸랑(3D, 고양이 거리). 0이면 끔")]
        [SerializeField, Range(0f, 1f)] private float _catBellJingleVolume = 0.35f;
        [SerializeField] private float _catBellStepMeters = 0.9f; // 이만큼 움직일 때마다 한 번

        public AudioClip CatBellJingleClip => Core.ToneSynth.Clink(2900f, 0.18f);
        public float CatBellJingleVolume => _catBellJingleVolume;
        public float CatBellStepMeters => _catBellStepMeters;

        public float HouseCueVolume => _houseCueVolume;
        public float HouseLoopVolume => _houseLoopVolume;
        public float CatBellVolume => _catBellVolume;

        public float CatMinDistance => _catMinDistance;
        public float CatMaxDistance => _catMaxDistance;
        public float SuspiciousVolume => _suspiciousVolume;
        public float ChaseVolume => _chaseVolume;
        public float CaptureVolume => _captureVolume;
        public float SnoreVolume => _snoreVolume;

        public float WorldMinDistance => _worldMinDistance;
        public float WorldMaxDistance => _worldMaxDistance;
        public float SqueakVolume => _squeakVolume;
        public float DepositVolume => _depositVolume;
        public float ImpactVolume(float loudness) => _impactVolume * Mathf.Clamp(loudness / 80f, 0.5f, 1f);
        public AudioClip SqueakClip => _squeakClip != null ? _squeakClip : Core.ToneSynth.Squeak(_squeakFreq, _squeakSeconds);
        public AudioClip ImpactClip => _impactClip != null ? _impactClip : Core.ToneSynth.Clink(_impactFreq, _impactSeconds);
        public AudioClip DepositClip => _depositClip != null ? _depositClip : Core.ToneSynth.Chime(_depositFrom, _depositTo, _depositSeconds);

        public AudioClip SuspiciousClip => _suspiciousClip != null ? _suspiciousClip : Core.ToneSynth.Chirp(_suspiciousFrom, _suspiciousTo, _suspiciousSeconds);
        public AudioClip ChaseClip => _chaseClip != null ? _chaseClip : Core.ToneSynth.Growl(_chaseFreq, _chaseTremolo, _chaseSeconds);
        public AudioClip CaptureClip => _captureClip != null ? _captureClip : Core.ToneSynth.Swipe(_captureSeconds);
        public AudioClip SnoreClip => _snoreClip != null ? _snoreClip : Core.ToneSynth.Snore(_snoreSeconds);
    }
}
