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
        [SerializeField] private float _lookDwellMultiplier = 1f;       // 관찰점에 머무는 시간

        [Header("특이 행동")]
        [SerializeField] private float _chaseGiveUpSeconds = 0f;        // >0이면 이 시간 넘게 쫓으면 하품하고 포기 (게으름뱅이)

        public string DisplayName => _displayName;
        public Color BodyColor => _bodyColor;
        public float ViewMultiplier => _viewMultiplier;
        public float HearingMultiplier => _hearingMultiplier;
        public float ChaseSpeedMultiplier => _chaseSpeedMultiplier;
        public float SleepDurationMultiplier => _sleepDurationMultiplier;
        public float BedWeightMultiplier => _bedWeightMultiplier;
        public float LookWeightMultiplier => _lookWeightMultiplier;
        public float LookDwellMultiplier => _lookDwellMultiplier;
        public float ChaseGiveUpSeconds => _chaseGiveUpSeconds;

#if UNITY_EDITOR
        public void EditorSetup(string name, Color color, float view, float hearing, float chaseSpeed,
                                float sleepDur, float bedWeight, float lookWeight, float lookDwell, float giveUp)
        {
            _displayName = name; _bodyColor = color; _viewMultiplier = view; _hearingMultiplier = hearing;
            _chaseSpeedMultiplier = chaseSpeed; _sleepDurationMultiplier = sleepDur; _bedWeightMultiplier = bedWeight;
            _lookWeightMultiplier = lookWeight; _lookDwellMultiplier = lookDwell; _chaseGiveUpSeconds = giveUp;
        }
#endif
    }
}
