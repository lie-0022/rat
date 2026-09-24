using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 고양이 성격 프로필 (design/cat-ideas/01). FSM은 같고 기존 수치에 곱하는 배율 + 특이 행동 한두 개.
    /// 프로필은 연출로 읽혀야 한다 — 몸 색이 그레이박스 표현. 새 프로필은 Data/Cats/에 에셋 추가만.
    /// </summary>
    [CreateAssetMenu(fileName = "CatPersonality", menuName = "RatGame/Cat Personality")]
    public class CatPersonalitySO : ScriptableObject
    {
        [SerializeField] private string _displayName = "고양이";
        [SerializeField] private Color _bodyColor = new(0.95f, 0.55f, 0.2f);

        [Header("감각·이동 배율")]
        [SerializeField] private float _viewMultiplier = 1f;
        [SerializeField] private float _hearingMultiplier = 1f;
        [SerializeField] private float _chaseSpeedMultiplier = 1f;

        [Header("잠·스팟 선호")]
        [SerializeField] private float _sleepDurationMultiplier = 1f;   // 잠자리 총 수면 시간
        [SerializeField] private float _bedWeightMultiplier = 1f;       // 잠자리를 다음 스팟으로 뽑을 확률
        [SerializeField] private float _lookWeightMultiplier = 1f;      // 관찰점 선호
        [SerializeField] private float _perchWeightMultiplier = 1f; // 선반(Perch) 스팟 선호 — 사냥꾼 3 (고양이 39)
        [SerializeField] private float _lookDwellMultiplier = 1f;       // 관찰점에 머무는 시간

        [Header("특이 행동")]
        [SerializeField] private float _chaseGiveUpSeconds = 0f;        // >0이면 이 시간 넘게 쫓으면 하품하고 포기 (게으름뱅이)
        [SerializeField] private float _curiositySpeedMultiplier = 1f;  // 굴러가는 물건 반응 속도 기준 × (호기심쟁이 0.5 — 천천히 굴러도)
        [SerializeField] private bool _curiousWhileSuspicious;          // 의심 중에도 굴러가는 물건에 속는다 (호기심쟁이)
        [SerializeField] private float _fleeLoudness = 0f;              // >0이면 이 이상 소리에 도망 (겁쟁이 60)
        [SerializeField] private float _fleeSeconds = 3f;
        [SerializeField] private bool _isKitten;                        // 아기 — 잡지 못하고(넘어뜨리기만) 놀라면 엄마를 부른다. 무작위 추첨 제외
        [SerializeField] private float _moveSpeedMultiplier = 1f;       // 모든 이동 속도 × (아기 0.75)
        [SerializeField] private float _bodyScale = 1f;                 // 몸 크기 (연출)
        [SerializeField] private float _wakeStretchSeconds = 0f;        // 깨서 움직이기 전 기지개 (게으름뱅이 1.5 — 텔레그래프)
        [SerializeField] private bool _isGuard;                         // 문지기 역할 전용 — 무작위 추첨 제외, 존 생성기가 줌 (고양이 82)

        public string DisplayName => _displayName;
        public Color BodyColor => _bodyColor;
        public float ViewMultiplier => _viewMultiplier;
        public float HearingMultiplier => _hearingMultiplier;
        public float ChaseSpeedMultiplier => _chaseSpeedMultiplier;
        public float SleepDurationMultiplier => _sleepDurationMultiplier;
        public float BedWeightMultiplier => _bedWeightMultiplier;
        public float LookWeightMultiplier => _lookWeightMultiplier;
        public float PerchWeightMultiplier => _perchWeightMultiplier;
        public float LookDwellMultiplier => _lookDwellMultiplier;
        public float ChaseGiveUpSeconds => _chaseGiveUpSeconds;
        public float CuriositySpeedMultiplier => _curiositySpeedMultiplier;
        public bool CuriousWhileSuspicious => _curiousWhileSuspicious;
        public float FleeLoudness => _fleeLoudness;
        public float FleeSeconds => _fleeSeconds;
        public bool IsKitten => _isKitten;
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;
        public float BodyScale => _bodyScale;
        public float WakeStretchSeconds => _wakeStretchSeconds;
        public bool IsGuard => _isGuard;
        /// <summary>무작위 성격 추첨에서 뺀다 (아기·문지기는 존 생성기가 역할로 준다).</summary>
        public bool RoleOnly => _isKitten || _isGuard;

#if UNITY_EDITOR
        public void EditorSetupPerch(float perchWeight) => _perchWeightMultiplier = perchWeight;
        public void EditorSetup(string name, Color color, float view, float hearing, float chaseSpeed,
                                float sleepDur, float bedWeight, float lookWeight, float lookDwell, float giveUp)
        {
            _displayName = name; _bodyColor = color; _viewMultiplier = view; _hearingMultiplier = hearing;
            _chaseSpeedMultiplier = chaseSpeed; _sleepDurationMultiplier = sleepDur; _bedWeightMultiplier = bedWeight;
            _lookWeightMultiplier = lookWeight; _lookDwellMultiplier = lookDwell; _chaseGiveUpSeconds = giveUp;
        }

        public void EditorSetupTraits(float curiositySpeedMul, bool curiousWhileSuspicious, float fleeLoudness, float fleeSeconds)
        {
            _curiositySpeedMultiplier = curiositySpeedMul; _curiousWhileSuspicious = curiousWhileSuspicious;
            _fleeLoudness = fleeLoudness; _fleeSeconds = fleeSeconds;
        }

        public void EditorSetupKitten(bool isKitten, float moveSpeedMul, float bodyScale)
        {
            _isKitten = isKitten; _moveSpeedMultiplier = moveSpeedMul; _bodyScale = bodyScale;
        }

        public void EditorSetupWakeStretch(float seconds) => _wakeStretchSeconds = seconds;
        public void EditorSetupGuard(bool isGuard) => _isGuard = isGuard;
#endif
    }
}
