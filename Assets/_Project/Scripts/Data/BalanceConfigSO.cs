using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 밸런스 수치 단일 SO (docs/02). 하드코딩 금지 — 새 수치는 여기 추가하고 해당 docs 표와 일치시킨다.
    /// 초기값 출처: docs/00-overview.md 핵심 수치 표. 고양이 수치는 BalanceConfigSO.CatFields.cs·BalanceConfigSO.Cat.cs.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "RatGame/Balance Config")]
    public partial class BalanceConfigSO : ScriptableObject
    {
        [Header("런 구성 (docs/00)")]
        [SerializeField] private int _maxPlayers = 4;
        [SerializeField] private int _maxZonesPerRun = 5;
        [SerializeField] private Vector2Int _roomModulesPerZone = new(3, 4);

        [Header("기지 출발·귀환 (docs/09·11)")]
        [SerializeField] private float _departCountdownSeconds = 3f; // 전원 출발 발판 집합 후 출발까지 (s)
        [SerializeField] private float _returnCountdownSeconds = 3f; // 전원 쥐구멍 집합 후 귀환까지 (s)
        [SerializeField] private float _resultScreenSeconds = 8f;    // 귀환·전멸 결과 화면 표시 시간 (s)

        [Header("새 루프 — 스테이지·할당량 (docs/09, 2026-09-24 잠정)")]
        [SerializeField] private int _stagesPerRun = 5;          // 이만큼 클리어하면 엔딩
        [SerializeField] private int _stageQuotaBase = 150;      // 1스테이지 식량 할당량
        [SerializeField] private int _stageQuotaPerStage = 75;   // 스테이지마다 더해지는 할당량

        [Header("솔로 보정")]
        [SerializeField, Range(0f, 1f)] private float _soloCatVisionMultiplier = 0.8f; // 시야 -20%

        [Header("플레이어 이동 (docs/04)")]
        [SerializeField] private float _walkSpeed = 3.8f;   // 2026-09-14 4.5 → 3.8 (기본 속도 낮춤, 상점 튼튼한 다리로 회복)
        [SerializeField] private float _sprintSpeed = 6f;   // 7 → 6
        [SerializeField] private float _crouchSpeed = 1.9f; // 2.2 → 1.9
        [SerializeField] private float _groundAcceleration = 40f;
        [SerializeField] private float _airAcceleration = 10f;
        [SerializeField] private float _jumpImpulse = 5.5f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBuffer = 0.1f;
        [SerializeField] private float _rotationSlerp = 12f;
        [SerializeField] private float _teleportGroundWaitSeconds = 5f; // 텔레포트 지점에 바닥이 아직 없으면(생성 스테이지 방 스폰 전) 이만큼 제자리 대기 (고양이 59)
        [SerializeField] private float _fallRescueY = -10f;             // 이보다 떨어지면 가까운 시작 위치로 (맵 밖 낙하 안전망)

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
        [SerializeField] private float _carryGripHeight = 0.78f;        // 대형 잡는 자리 높이(바닥에서, m) — 쥐 몸 앵커 높이와 같게. 윗면에 두면 키 큰 물건(통닭 1.6m)에서 관절이 쥐를 들어 올려 호스트 쥐가 떠서 못 걸었다 (고양이 213)
        [SerializeField] private int _largeCarrySlots = 2;              // 대형(8~12kg) 잡는 자리 — docs/05 표 "대형 grip 2~3"
        [SerializeField] private float _extraLargeMass = 16f;           // 이 무게 이상 대형 = 특대 (docs/05 "특대 16~24") — 등급이 아니라 무게로 (통닭·수박은 등급 Large)
        [SerializeField] private int _extraLargeCarrySlots = 4;         // 특대 잡는 자리 — docs/05 "특대 grip 4, 권장 3~4" (고양이 212)
        [SerializeField] private float _sprintBlockLoad = 3f;
        [SerializeField] private float _jumpBlockLoad = 4.5f;
        // 던지기는 "속도" 기준 — 임펄스 고정이면 가벼운 물건이 총알이 된다 (치즈 0.6kg → 15m/s 사고)
        [SerializeField] private float _throwSpeedMin = 2f;   // m/s, 차지 0
        [SerializeField] private float _throwSpeedMax = 6f;   // m/s, 만차지
        [SerializeField] private float _throwMassDampNumerator = 3f; // massDamp = Clamp01(3/mass)
        [SerializeField] private float _throwChargeTime = 1.2f;      // 홀드 만충 시간
        [SerializeField] private float _throwHitStagger = 0.5f;      // 맞은 플레이어 비틀거림
        [SerializeField] private float _throwAimMinDistance = 1.5f;  // 1인칭 조준 수렴 거리 하한 (m) — 코앞 벽이면 손에서 옆으로 꺾이지 않게
        [SerializeField] private float _throwAimMaxDistance = 6f;    // 상한 (m) — 조준선이 아무것도 안 맞으면 이 거리로 모은다

        [Header("소음 (docs/06)")]
        [SerializeField] private float _maxNoiseRadius = 14f;        // loudness 100 기준 전파 반경
        [SerializeField] private float _wallAttenuation = 0.6f;      // NoiseBlocker 1장 통과 시 ×0.6 (−40%)
        [SerializeField] private float _footstepWalkLoudness = 8f;
        [SerializeField] private float _footstepRunLoudness = 22f;
        [SerializeField] private float _footstepWalkInterval = 0.35f;
        [SerializeField] private float _footstepRunInterval = 0.25f;
        [SerializeField] private float _pipeEchoMultiplier = 1.6f;   // 배관 안 달리기 발소리 배율 (쇠관 울림, 고양이 87)
        [SerializeField] private float _landingBaseLoudness = 15f;   // + 낙하높이 × 5, 1m 이상만
        [SerializeField] private float _landingPerMeter = 5f;
        [SerializeField] private float _squeakLoudness = 30f;
        [SerializeField] private float _squeakHearMeters = 30f;       // 동료가 찍찍 자막을 보는 거리 (고양이 158 — 오디오 전 자막)
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
        [Header("함정 (docs/10, 고양이 49)")]
        [SerializeField] private float _trapMouseLoudness = 80f;   // 쥐덫 격발 (docs/06)
        [SerializeField] private float _glueGraceSeconds = 4f;     // 구출된 쥐가 같은 끈끈이에 다시 안 붙는 시간
        [SerializeField] private float _wireOnSeconds = 1.5f;      // 전기선 켜짐
        [SerializeField] private float _wireOffSeconds = 1.5f;     // 꺼짐 — 이 틈에 지나간다
        [SerializeField] private float _wireStunSeconds = 2f;
        [SerializeField] private float _wireZapLoudness = 30f;
        [SerializeField] private float _trapBaitStunSeconds = 2.5f; // 쥐덫 미끼를 서서 집으면 (고양이 129)
        [SerializeField] private float _trapBaitHintMeters = 3f;    // 이 안에 처음 들어오면 미끼 규칙 안내 (고양이 132)
        [SerializeField] private float _catBellHitMeters = 1f;      // 던진 방울이 떨어진 자리 이 안 고양이에 달림 (고양이 140)
        [Header("큰 고양이 발걸음 (고양이 145)")]
        [SerializeField] private float _catStepShakeRadius = 10f;      // 이 안의 큰 고양이 걸음만 느낀다
        [SerializeField] private float _catStepShakeAmplitude = 0.05f; // 바로 옆에서 시점이 내려앉는 최대 (m)
        [SerializeField] private float _catStrideMeters = 1.4f;        // 큰 고양이 걸음 폭 (몸 배율 1.9 기준, 배율에 비례)
        [SerializeField] private float _catStepShakeDecaySeconds = 0.12f;

        [Header("핑 (docs/04)")]
        [SerializeField] private float _pingItemSnapMeters = 0.6f; // 핑 지점에서 이 안의 물건이면 이름·가치 표시 (고양이 139)
        [SerializeField] private float _pingCooldownSeconds = 1f; // 같은 쥐의 다음 핑까지 (서버도 검사)
        [SerializeField] private float _pingMarkerSeconds = 3f;   // 마커 표시 시간
        [SerializeField] private float _pingMaxDistance = 30f;    // 조준 레이 최대 거리 (m) — 아무것도 안 맞으면 이 거리 지점
        [SerializeField] private float _sniffCooldownSeconds = 10f; // 킁킁 다음까지 (docs/04 Sniff, 고양이 76)
        [SerializeField] private float _sniffTrailSeconds = 3f;     // 냄새 줄기 보이는 시간
        [SerializeField] private float _sniffFoodMeters = 14f;      // 킁킁 — 이 안의 음식에서 냄새 김 (고양이 150)
        [SerializeField] private int _sniffFoodMax = 8;             // 가까운 순 최대 개수
        [SerializeField] private Vector2 _sniffFoodTierValues = new(30f, 80f); // 값이 이 둘 미만/사이/이상 → 김 3·4·6알 (고양이 160)

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
        [SerializeField] private float _eatSeconds = 1f;           // 치즈 먹기 E 길게 (docs/04, 고양이 48)
        [SerializeField] private float _eatStamina = 50f;          // 즉시 회복량
        [SerializeField] private float _staminaRegenDelay = 1f;
        [SerializeField] private float _exhaustPantSeconds = 1.5f; // 0 도달 시 헐떡임

        [Header("업그레이드 (docs/11 상점) — 레벨당 가산")]
        [SerializeField] private int _baseCarrySlots = 2;                 // 인벤 기본 칸 (2026-09-12 결정)
        [SerializeField] private float _upgradeMoveSpeedPerLevel = 0.08f; // 걷기·달리기 +8%/Lv
        [SerializeField] private float _upgradeStaminaPerLevel = 0.2f;    // 최대 스태미나 +20%/Lv
        [SerializeField] private float _upgradeThrowPerLevel = 0.15f;     // 던지기 속도 +15%/Lv
        [SerializeField] private float _upgradeJumpPerLevel = 0.1f;       // 점프 임펄스 +10%/Lv (높이는 제곱이라 약 +21%/Lv)
        [SerializeField] private float _pantLoudness = 18f;

        public int MaxPlayers => _maxPlayers;
        public int MaxZonesPerRun => _maxZonesPerRun;
        public Vector2Int RoomModulesPerZone => _roomModulesPerZone;
        public float SoloCatVisionMultiplier => _soloCatVisionMultiplier;
        public float DepartCountdownSeconds => _departCountdownSeconds;
        public float ReturnCountdownSeconds => _returnCountdownSeconds;
        public float ResultScreenSeconds => _resultScreenSeconds;
        public int StagesPerRun => _stagesPerRun;
        public int StageQuota(int stage) => _stageQuotaBase + _stageQuotaPerStage * Mathf.Max(0, stage - 1);

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CrouchSpeed => _crouchSpeed;
        public float GroundAcceleration => _groundAcceleration;
        public float AirAcceleration => _airAcceleration;
        public float JumpImpulse => _jumpImpulse;
        public float CoyoteTime => _coyoteTime;
        public float JumpBuffer => _jumpBuffer;
        public float RotationSlerp => _rotationSlerp;
        public float TeleportGroundWaitSeconds => _teleportGroundWaitSeconds;
        public float FallRescueY => _fallRescueY;

        public float GrabSpring => _grabSpring;
        public float GrabDamper => _grabDamper;
        public float GrabMaxForce => _grabMaxForce;
        public float GrabAngularSpring => _grabAngularSpring;
        public float GrabRange => _grabRange;
        public float GrabBreakDistance => _grabBreakDistance;
        public float CarrySlotStandDistance => _carrySlotStandDistance;
        public float CarryGripHeight => _carryGripHeight;
        /// <summary>대형 잡는 자리 수 — 특대(무게 extraLargeMass 이상)는 4, 아니면 2 (docs/05 표, 고양이 212).</summary>
        public int HeavyCarrySlots(float mass) => mass >= _extraLargeMass ? _extraLargeCarrySlots : _largeCarrySlots;
        public float CarrySlotSnapTime => _carrySlotSnapTime;
        public float SprintBlockLoad => _sprintBlockLoad;
        public float JumpBlockLoad => _jumpBlockLoad;
        public float ThrowSpeedMin => _throwSpeedMin;
        public float ThrowSpeedMax => _throwSpeedMax;
        public float ThrowChargeTime => _throwChargeTime;
        public float ThrowHitStagger => _throwHitStagger;
        public float ThrowAimMinDistance => _throwAimMinDistance;
        public float ThrowAimMaxDistance => _throwAimMaxDistance;

        public float MaxNoiseRadius => _maxNoiseRadius;
        public float WallAttenuation => _wallAttenuation;
        public float FootstepWalkLoudness => _footstepWalkLoudness;
        public float PipeEchoMultiplier => _pipeEchoMultiplier;
        public float FootstepRunLoudness => _footstepRunLoudness;
        public float FootstepWalkInterval => _footstepWalkInterval;
        public float FootstepRunInterval => _footstepRunInterval;
        public float SqueakLoudness => _squeakLoudness;
        public float SqueakHearMeters => _squeakHearMeters;
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

        public float PingCooldownSeconds => _pingCooldownSeconds;
        public float PingMarkerSeconds => _pingMarkerSeconds;
        public float PingItemSnapMeters => _pingItemSnapMeters;
        public float PingMaxDistance => _pingMaxDistance;
        public float SniffCooldownSeconds => _sniffCooldownSeconds;
        public float SniffTrailSeconds => _sniffTrailSeconds;
        public float SniffFoodMeters => _sniffFoodMeters;
        public int SniffFoodMax => _sniffFoodMax;
        public Vector2 SniffFoodTierValues => _sniffFoodTierValues;

        /// <summary>파손 데미지: 문턱 초과분 × 0.15 (docs/05).</summary>
        public float GetFragileDamage(float impactSpeed) =>
            impactSpeed <= _fragileBreakSpeed ? 0f : (impactSpeed - _fragileBreakSpeed) * _fragileDamagePerSpeed;

        public float StunSeconds => _stunSeconds;

        public int BaseCarrySlots => _baseCarrySlots;
        public float UpgradeMoveSpeedPerLevel => _upgradeMoveSpeedPerLevel;
        public float UpgradeStaminaPerLevel => _upgradeStaminaPerLevel;
        public float UpgradeThrowPerLevel => _upgradeThrowPerLevel;
        public float UpgradeJumpPerLevel => _upgradeJumpPerLevel;
        public float HighFallStunHeight => _highFallStunHeight;
        public float RescueHoldSeconds => _rescueHoldSeconds;
        public float TrapMouseLoudness => _trapMouseLoudness;
        public float GlueGraceSeconds => _glueGraceSeconds;
        public float WireOnSeconds => _wireOnSeconds;
        public float WireOffSeconds => _wireOffSeconds;
        public float WireStunSeconds => _wireStunSeconds;
        public float WireZapLoudness => _wireZapLoudness;
        public float TrapBaitStunSeconds => _trapBaitStunSeconds;
        public float TrapBaitHintMeters => _trapBaitHintMeters;
        public float CatBellHitMeters => _catBellHitMeters;
        public float CatStepShakeRadius => _catStepShakeRadius;
        public float CatStepShakeAmplitude => _catStepShakeAmplitude;
        public float CatStrideMeters => _catStrideMeters;
        public float CatStepShakeDecaySeconds => _catStepShakeDecaySeconds;
        public float StaminaMax => _staminaMax;
        public float EatSeconds => _eatSeconds;
        public float EatStamina => _eatStamina;
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

        /// <summary>
        /// 큰 고양이용 복사본 (고양이 141 — 실제 비율): 몸·공간에 묶인 거리만 배율을 곱한다(잡기·앞발·시야·수색·놀이 거리 등).
        /// 속도는 그대로 — "추격 5.5 &lt; 쥐 달리기 7"이 밸런스의 축이라서. 호스트가 고양이를 스폰하기 전에 만들어 그 고양이에만 준다.
        /// </summary>
        public BalanceConfigSO ScaledForCat(float s)
        {
            var c = Instantiate(this);
            c.name = $"{name} (고양이 ×{s:0.##})";
            c._catCaptureRange *= s; c._catCaptureRadius *= s; c._catCloseSightDistance *= s; c._catPawReach *= s;
            c._catBribeRadius *= s; c._catToyNudge *= s; c._catToyEscapeDistance *= s; c._catChaseOvershootMeters *= s;
            c._catSearchRadius *= s; c._catMemoryCellSize *= s; c._catViewDistance *= s; c._catPerchViewDistance *= s;
            c._catScentDetectRadius *= s; c._catToyCarryMinDistance *= s; c._catToyCarryMaxDistance *= s;
            c._catAmbushPounceRange *= s; c._catFightDistance *= s; c._catGuardRadius *= s;
            return c;
        }
    }
}
