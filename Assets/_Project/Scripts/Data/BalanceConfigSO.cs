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

        [Header("할당량 곡선 — Zone n 할당량 = base × growth^(n-1)")]
        [SerializeField] private int _quotaBase = 100;
        [SerializeField] private float _quotaGrowth = 1.6f;

        [Header("솔로 보정")]
        [SerializeField, Range(0f, 1f)] private float _soloQuotaMultiplier = 0.55f;
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
        [SerializeField] private float _sprintBlockLoad = 3f;
        [SerializeField] private float _jumpBlockLoad = 4.5f;
        [SerializeField] private float _throwForceMin = 2f;
        [SerializeField] private float _throwForceMax = 9f;
        [SerializeField] private float _throwMassDampNumerator = 3f; // massDamp = Clamp01(3/mass)
        [SerializeField] private float _throwChargeTime = 1.2f;      // 홀드 만충 시간
        [SerializeField] private float _throwHitStagger = 0.5f;      // 맞은 플레이어 비틀거림

        public int MaxPlayers => _maxPlayers;
        public int MaxZonesPerRun => _maxZonesPerRun;
        public Vector2Int RoomModulesPerZone => _roomModulesPerZone;
        public float SoloQuotaMultiplier => _soloQuotaMultiplier;
        public float SoloCatVisionMultiplier => _soloCatVisionMultiplier;

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
        public float SprintBlockLoad => _sprintBlockLoad;
        public float JumpBlockLoad => _jumpBlockLoad;
        public float ThrowForceMin => _throwForceMin;
        public float ThrowForceMax => _throwForceMax;
        public float ThrowChargeTime => _throwChargeTime;
        public float ThrowHitStagger => _throwHitStagger;

        /// <summary>1인당 하중 → 이동 속도 배수 (docs/05 공식).</summary>
        public float GetCarrySpeedMultiplier(float loadPerRat) =>
            Mathf.Clamp01(1.2f - loadPerRat / _heavyThreshold);

        /// <summary>던지기 임펄스 크기 (docs/05 — 무거운 건 못 던진다).</summary>
        public float GetThrowImpulse(float charge, float mass) =>
            Mathf.Lerp(_throwForceMin, _throwForceMax, charge) * Mathf.Clamp01(_throwMassDampNumerator / mass);

        /// <summary>Zone n(1부터)의 할당량. 솔로면 solo 배수 적용 (docs/00 표).</summary>
        public int GetQuota(int zoneNumber, int playerCount)
        {
            float quota = _quotaBase * Mathf.Pow(_quotaGrowth, zoneNumber - 1);
            if (playerCount <= 1) quota *= _soloQuotaMultiplier;
            return Mathf.RoundToInt(quota);
        }

        /// <summary>구역별 고양이 수: Zone1: 1, Zone3+: 2 (docs/00 표).</summary>
        public int GetCatCount(int zoneNumber) => zoneNumber >= 3 ? 2 : 1;
    }
}
