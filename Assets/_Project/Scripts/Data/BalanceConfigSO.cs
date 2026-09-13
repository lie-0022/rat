using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 밸런스 수치 단일 SO (docs/02). 하드코딩 금지 — 새 수치는 여기 추가하고 해당 docs 표와 일치시킨다.
    /// 초기값 출처: docs/00-overview.md 핵심 수치 표.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "RatGame/Balance Config")]
    public class BalanceConfigSO : ScriptableObject
    {
        [Header("런 구성 (docs/00)")]
        [SerializeField] private int _maxPlayers = 4;
        [SerializeField] private int _maxZonesPerRun = 5;
        [SerializeField] private Vector2Int _roomModulesPerZone = new(3, 4);

        [Header("귀환 (docs/09)")]
        [SerializeField] private float _returnCountdownSeconds = 3f; // 전원 쥐구멍 집합 후 귀환까지 (s)
        [SerializeField] private float _resultScreenSeconds = 8f;    // 귀환·전멸 결과 화면 표시 시간 (s)

        [Header("솔로 보정")]
        [SerializeField, Range(0f, 1f)] private float _soloCatVisionMultiplier = 0.8f; // 시야 -20%

        [Header("플레이어 이동 (docs/04)")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _crouchSpeed = 2.2f;
        [SerializeField] private float _groundAcceleration = 40f;
        [SerializeField] private float _airAcceleration = 10f;
        [SerializeField] private float _jumpImpulse = 5.5f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBuffer = 0.1f;
        [SerializeField] private float _rotationSlerp = 12f;

        [Header("운반 (docs/05)")]
        [SerializeField] private float _grabSpring = 600f;
        [SerializeField] private float _grabDamper = 40f;
        [SerializeField] private float _grabMaxForce = 80f;   // mass 무관 고정 — 무거우면 끌리는 게 의도
        [SerializeField] private float _grabAngularSpring = 50f;
        [SerializeField] private float _grabRange = 1.2f;     // 호스트 검증 관용치
        [SerializeField] private float _grabBreakDistance = 3f;
        [SerializeField] private float _heavyThreshold = 6f;
        [SerializeField, Range(0f, 0.5f)] private float _carryMinSpeedMultiplier = 0.15f; // 혼자 대형 끌 때 기어가는 최저 속도 (0이면 정지 — 재미 없음)
        [SerializeField] private float _carrySlotStandDistance = 0.45f; // 자동 대형: 물건 면에서 쥐 몸 중심까지 (m). 쥐 반경 0.3 + 여유
        [SerializeField] private float _carrySlotSnapTime = 0.2f;       // 자동 대형: 자리로 붙는 데 걸리는 시간 (s)
        [SerializeField] private float _sprintBlockLoad = 3f;
        [SerializeField] private float _jumpBlockLoad = 4.5f;
        // 던지기는 "속도" 기준 — 임펄스 고정이면 가벼운 물건이 총알이 된다 (치즈 0.6kg → 15m/s 사고)
        [SerializeField] private float _throwSpeedMin = 2f;   // m/s, 차지 0
        [SerializeField] private float _throwSpeedMax = 6f;   // m/s, 만차지
        [SerializeField] private float _throwMassDampNumerator = 3f; // massDamp = Clamp01(3/mass)
        [SerializeField] private float _throwChargeTime = 1.2f;      // 홀드 만충 시간
        [SerializeField] private float _throwHitStagger = 0.5f;      // 맞은 플레이어 비틀거림

        [Header("소음 (docs/06)")]
        [SerializeField] private float _maxNoiseRadius = 14f;        // loudness 100 기준 전파 반경
        [SerializeField] private float _wallAttenuation = 0.6f;      // NoiseBlocker 1장 통과 시 ×0.6 (−40%)
        [SerializeField] private float _footstepWalkLoudness = 8f;
        [SerializeField] private float _footstepRunLoudness = 22f;
        [SerializeField] private float _footstepWalkInterval = 0.35f;
        [SerializeField] private float _footstepRunInterval = 0.25f;
        [SerializeField] private float _landingBaseLoudness = 15f;   // + 낙하높이 × 5, 1m 이상만
        [SerializeField] private float _landingPerMeter = 5f;
        [SerializeField] private float _squeakLoudness = 30f;
        [SerializeField] private float _impactMaxLoudness = 70f;
        [SerializeField] private float _breakLoudness = 60f;
        [SerializeField] private float _alarmingLoudness = 50f;
        [SerializeField] private float _rippleThreshold = 40f;       // 이상이면 파문 이펙트

        [Header("트레잇 (docs/05)")]
        [SerializeField] private float _fragileBreakSpeed = 5f;      // 이 속도(m/s) 초과 충돌만 파손
        [SerializeField] private float _fragileDamagePerSpeed = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _crackedValueMultiplier = 0.5f; // Durability<0.5
        [SerializeField] private float _slipperyInterval = 3f;
        [SerializeField, Range(0f, 1f)] private float _slipperyChance = 0.12f;

        [Header("상태이상·구출 (docs/04)")]
        [SerializeField] private float _stunSeconds = 2f;
        [SerializeField] private float _highFallStunHeight = 6f;   // 이 높이(m) 초과 낙하 → Stunned
        [SerializeField] private float _rescueHoldSeconds = 1.5f;  // Trapped 동료 구출 홀드

        [Header("고양이 (docs/07)")]
        [SerializeField] private float _catPatrolSpeed = 2f;
        [SerializeField] private float _catSuspiciousSpeed = 3f;
        [SerializeField] private float _catChaseSpeed = 5.5f;   // sprint 7보다 느림 — 밸런스의 축, 신중히
        [SerializeField] private float _catReturnSpeed = 2f;
        [SerializeField] private float _catViewDistance = 8f;
        [SerializeField, Range(0f, 1f)] private float _catCrouchViewMultiplier = 0.5f;
        [SerializeField] private float _catViewHalfAngle = 35f; // 시야각 70°
        [SerializeField] private float _catGazeGainPerSec = 60f;
        [SerializeField] private float _catGaugeDecayPerSec = 20f;
        [SerializeField, Range(0f, 1f)] private float _catHearingGain = 0.8f;
        [SerializeField] private float _catHearThreshold = 10f;
        [SerializeField] private float _catSuspicionThreshold = 30f;
        [SerializeField] private float _catChaseThreshold = 100f;
        [SerializeField] private float _catDirectSightChaseSeconds = 1f;
        [SerializeField] private float _catCloseSightDistance = 2f;
        [SerializeField] private float _catLoseSightSeconds = 3f;
        [SerializeField] private float _catCaptureRange = 1.0f;
        [SerializeField] private float _catCaptureSwingSeconds = 0.4f;
        [SerializeField] private float _catCaptureRadius = 1.2f;
        [SerializeField] private float _catGroomSeconds = 3f;
        [SerializeField] private float _catSuspiciousWanderSeconds = 6f;
        [SerializeField, Range(0f, 1f)] private float _catSleepSenseMultiplier = 0.3f;
        [SerializeField] private Vector2 _catPatrolWaitRange = new(2f, 5f);
        [SerializeField] private float _catDistractedSpeed = 4f;

        [Header("유인 아이템 (docs/07·08)")]
        [SerializeField] private float _lureYarnSeconds = 8f;
        [SerializeField] private float _lureYarnRadius = 10f;   // 낙하지점 기준 유인 범위
        [SerializeField] private float _lureCatnipSeconds = 15f;
        [SerializeField] private float _lureCatnipRadius = 3f;

        [Header("스태미나 (docs/04)")]
        [SerializeField] private float _staminaMax = 100f;
        [SerializeField] private float _staminaSprintDrain = 20f;  // /s
        [SerializeField] private float _staminaHeavyDrain = 15f;   // /s — 무거운 운반(sprint 차단 하중)
        [SerializeField] private float _staminaRegen = 25f;        // /s, 미소모 1s 후
        [SerializeField] private float _staminaRegenDelay = 1f;
        [SerializeField] private float _exhaustPantSeconds = 1.5f; // 0 도달 시 헐떡임
        [SerializeField] private float _pantLoudness = 18f;

        public int MaxPlayers => _maxPlayers;
        public int MaxZonesPerRun => _maxZonesPerRun;
        public Vector2Int RoomModulesPerZone => _roomModulesPerZone;
        public float SoloCatVisionMultiplier => _soloCatVisionMultiplier;
        public float ReturnCountdownSeconds => _returnCountdownSeconds;
        public float ResultScreenSeconds => _resultScreenSeconds;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CrouchSpeed => _crouchSpeed;
        public float GroundAcceleration => _groundAcceleration;
        public float AirAcceleration => _airAcceleration;
        public float JumpImpulse => _jumpImpulse;
        public float CoyoteTime => _coyoteTime;
        public float JumpBuffer => _jumpBuffer;
        public float RotationSlerp => _rotationSlerp;

        public float GrabSpring => _grabSpring;
        public float GrabDamper => _grabDamper;
        public float GrabMaxForce => _grabMaxForce;
        public float GrabAngularSpring => _grabAngularSpring;
        public float GrabRange => _grabRange;
        public float GrabBreakDistance => _grabBreakDistance;
        public float CarrySlotStandDistance => _carrySlotStandDistance;
        public float CarrySlotSnapTime => _carrySlotSnapTime;
        public float SprintBlockLoad => _sprintBlockLoad;
        public float JumpBlockLoad => _jumpBlockLoad;
        public float ThrowSpeedMin => _throwSpeedMin;
        public float ThrowSpeedMax => _throwSpeedMax;
        public float ThrowChargeTime => _throwChargeTime;
        public float ThrowHitStagger => _throwHitStagger;

        public float MaxNoiseRadius => _maxNoiseRadius;
        public float WallAttenuation => _wallAttenuation;
        public float FootstepWalkLoudness => _footstepWalkLoudness;
        public float FootstepRunLoudness => _footstepRunLoudness;
        public float FootstepWalkInterval => _footstepWalkInterval;
        public float FootstepRunInterval => _footstepRunInterval;
        public float SqueakLoudness => _squeakLoudness;
        public float ImpactMaxLoudness => _impactMaxLoudness;
        public float BreakLoudness => _breakLoudness;
        public float AlarmingLoudness => _alarmingLoudness;
        public float RippleThreshold => _rippleThreshold;

        /// <summary>착지 소음: 낙하 1m 미만은 0 (docs/06).</summary>
        public float GetLandingLoudness(float fallHeight) =>
            fallHeight < 1f ? 0f : _landingBaseLoudness + fallHeight * _landingPerMeter;

        /// <summary>아이템 충돌 소음: impact × mass계수(0.5~3), 상한 70 (docs/06).</summary>
        public float GetImpactLoudness(float impactSpeed, float mass) =>
            Mathf.Min(impactSpeed * Mathf.Clamp(mass, 0.5f, 3f), _impactMaxLoudness);

        public float FragileBreakSpeed => _fragileBreakSpeed;
        public float CrackedValueMultiplier => _crackedValueMultiplier;
        public float SlipperyInterval => _slipperyInterval;
        public float SlipperyChance => _slipperyChance;

        /// <summary>파손 데미지: 문턱 초과분 × 0.15 (docs/05).</summary>
        public float GetFragileDamage(float impactSpeed) =>
            impactSpeed <= _fragileBreakSpeed ? 0f : (impactSpeed - _fragileBreakSpeed) * _fragileDamagePerSpeed;

        public float CatPatrolSpeed => _catPatrolSpeed;
        public float CatSuspiciousSpeed => _catSuspiciousSpeed;
        public float CatChaseSpeed => _catChaseSpeed;
        public float CatReturnSpeed => _catReturnSpeed;
        public float CatViewDistance => _catViewDistance;
        public float CatCrouchViewMultiplier => _catCrouchViewMultiplier;
        public float CatViewHalfAngle => _catViewHalfAngle;
        public float CatGazeGainPerSec => _catGazeGainPerSec;
        public float CatGaugeDecayPerSec => _catGaugeDecayPerSec;
        public float CatHearingGain => _catHearingGain;
        public float CatHearThreshold => _catHearThreshold;
        public float CatSuspicionThreshold => _catSuspicionThreshold;
        public float CatChaseThreshold => _catChaseThreshold;
        public float CatDirectSightChaseSeconds => _catDirectSightChaseSeconds;
        public float CatCloseSightDistance => _catCloseSightDistance;
        public float CatLoseSightSeconds => _catLoseSightSeconds;
        public float CatCaptureRange => _catCaptureRange;
        public float CatCaptureSwingSeconds => _catCaptureSwingSeconds;
        public float CatCaptureRadius => _catCaptureRadius;
        public float CatGroomSeconds => _catGroomSeconds;
        public float CatSuspiciousWanderSeconds => _catSuspiciousWanderSeconds;
        public float CatSleepSenseMultiplier => _catSleepSenseMultiplier;
        public Vector2 CatPatrolWaitRange => _catPatrolWaitRange;
        public float CatDistractedSpeed => _catDistractedSpeed;
        public float LureYarnSeconds => _lureYarnSeconds;
        public float LureYarnRadius => _lureYarnRadius;
        public float LureCatnipSeconds => _lureCatnipSeconds;
        public float LureCatnipRadius => _lureCatnipRadius;

        public float StunSeconds => _stunSeconds;
        public float HighFallStunHeight => _highFallStunHeight;
        public float RescueHoldSeconds => _rescueHoldSeconds;
        public float StaminaMax => _staminaMax;
        public float StaminaSprintDrain => _staminaSprintDrain;
        public float StaminaHeavyDrain => _staminaHeavyDrain;
        public float StaminaRegen => _staminaRegen;
        public float StaminaRegenDelay => _staminaRegenDelay;
        public float ExhaustPantSeconds => _exhaustPantSeconds;
        public float PantLoudness => _pantLoudness;

        /// <summary>1인당 하중 → 이동 속도 배수 (docs/05 공식).</summary>
        public float GetCarrySpeedMultiplier(float loadPerRat) =>
            Mathf.Clamp(1.2f - loadPerRat / _heavyThreshold, _carryMinSpeedMultiplier, 1f);

        /// <summary>던지기 임펄스 크기 (docs/05 — 무거운 건 못 던진다).</summary>
        /// <summary>발사 속도 (m/s). 무거울수록 감쇠 — docs/05 "무거운 건 못 던진다".</summary>
        public float GetThrowSpeed(float charge, float mass) =>
            Mathf.Lerp(_throwSpeedMin, _throwSpeedMax, charge) * Mathf.Clamp01(_throwMassDampNumerator / mass);

        /// <summary>속도를 임펄스로 환산 (AddForce Impulse용) — 질량과 무관하게 같은 속도로 날아간다.</summary>
        public float GetThrowImpulse(float charge, float mass) => GetThrowSpeed(charge, mass) * mass;


        /// <summary>구역별 고양이 수: Zone1: 1, Zone3+: 2 (docs/00 표).</summary>
        public int GetCatCount(int zoneNumber) => zoneNumber >= 3 ? 2 : 1;
    }
}
