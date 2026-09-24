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

        [Header("기지 출발·귀환 (docs/09·11)")]
        [SerializeField] private float _departCountdownSeconds = 3f; // 전원 출발 발판 집합 후 출발까지 (s)
        [SerializeField] private float _returnCountdownSeconds = 3f; // 전원 쥐구멍 집합 후 귀환까지 (s)
        [SerializeField] private float _resultScreenSeconds = 8f;    // 귀환·전멸 결과 화면 표시 시간 (s)

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
        [SerializeField] private float _throwAimMinDistance = 1.5f;  // 1인칭 조준 수렴 거리 하한 (m) — 코앞 벽이면 손에서 옆으로 꺾이지 않게
        [SerializeField] private float _throwAimMaxDistance = 6f;    // 상한 (m) — 조준선이 아무것도 안 맞으면 이 거리로 모은다

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

        [Header("핑 (docs/04)")]
        [SerializeField] private float _pingCooldownSeconds = 1f; // 같은 쥐의 다음 핑까지 (서버도 검사)
        [SerializeField] private float _pingMarkerSeconds = 3f;   // 마커 표시 시간
        [SerializeField] private float _pingMaxDistance = 30f;    // 조준 레이 최대 거리 (m) — 아무것도 안 맞으면 이 거리 지점

        [Header("고양이 (docs/07)")]
        [SerializeField] private float _catPatrolSpeed = 2f;
        [SerializeField] private float _catSuspiciousSpeed = 3f;
        [SerializeField] private float _catChaseSpeed = 5.5f;   // sprint 7보다 느림 — 밸런스의 축, 신중히
        [SerializeField] private float _catReturnSpeed = 2f;
        [SerializeField] private float _catViewDistance = 8f;
        [SerializeField, Range(0f, 1f)] private float _catCrouchViewMultiplier = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _catDarkViewMultiplier = 0.5f; // 어둠 구역(LightZone) 속 쥐 (docs/07)
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
        [SerializeField] private float _catSuspiciousTravelSeconds = 10f; // 조사 지점까지 가는 길 상한 (배회 6s는 도착 뒤부터)
        [SerializeField, Range(0f, 1f)] private float _catSleepSenseMultiplier = 0.3f;
        [SerializeField] private Vector2 _catPatrolWaitRange = new(2f, 5f);
        [SerializeField] private float _catDistractedSpeed = 4f;

        [Header("고양이 스팟 (design/cat-design/02) — 머무는 시간 s / 그동안 감각 배율")]
        [SerializeField] private float _catBedSleepSeconds = 60f;         // 잠자리에서 잠드는 시간 (깨면 순찰)
        [SerializeField] private float _catSpotFoodSeconds = 25f;
        [SerializeField, Range(0f, 1f)] private float _catSpotFoodSense = 0.5f;   // 챱챱 소리에 묻힘
        [SerializeField] private float _catSpotSunSeconds = 40f;
        [SerializeField, Range(0f, 1f)] private float _catSpotSunSense = 0.5f;    // 반쯤 잠
        [SerializeField] private float _catSpotGroomSeconds = 20f;
        [SerializeField, Range(0f, 1f)] private float _catSpotGroomSense = 0.6f;
        [SerializeField] private int _catSpotAvoidRecent = 2;              // 최근 n개 스팟은 다시 안 뽑음

        [Header("고양이 잠의 단계 (design/cat-ideas/08) — 얕은 잠 ↔ 깊은 잠 파동")]
        [SerializeField] private Vector2 _catSleepLightRange = new(12f, 20f);   // 얕은 잠 지속 (s)
        [SerializeField] private Vector2 _catSleepDeepRange = new(10f, 18f);    // 깊은 잠 지속 (s)
        [SerializeField] private float _catSleepForecastSeconds = 3f;          // 단계 전환 예고 (꼬리 씰룩)
        [SerializeField, Range(0f, 1f)] private float _catSleepLightSense = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _catSleepDeepSense = 0.15f;
        [SerializeField] private float _catHalfAwakeSeconds = 3f;              // 한쪽 눈 뜸 — 더 자극 없으면 다시 잔다
        [SerializeField, Range(0f, 1f)] private float _catHalfAwakeSense = 0.5f;
        [SerializeField] private float _catDeepWakeGaugeMul = 2f;              // 깊은 잠은 의심 임계 ×2여야 반응

        [Header("고양이 추격 (design/cat-design/01-3) — 예측·오버슛·코너 감속")]
        [SerializeField] private float _catChaseLeadSeconds = 1f;          // 타깃 속도로 이만큼 앞을 노린다
        [SerializeField] private float _catChaseOvershootMeters = 3f;      // 시야를 잃으면 마지막 진행 방향으로 이만큼 더 가 본다
        [SerializeField, Range(0.2f, 1f)] private float _catChaseCornerSpeedMul = 0.55f; // 방향을 크게 틀 때 속도 배율 (지그재그가 통하게)

        [Header("숨을 곳·수색 (design/cat-ideas/14)")]
        [SerializeField] private float _hideExitNoise = 35f;             // 숨은 곳에서 나올 때 부스럭 — 반경 ≈4.9m (걷기 8·달리기 22 기준, docs/06)
        [SerializeField] private float _catSearchRadius = 6f;            // 마지막 목격점 주변 수색 반경
        [SerializeField] private float _catSearchSeconds = 25f;          // 수색 최대 시간
        [SerializeField] private int _catSearchMaxSpots = 3;
        [SerializeField] private float _catSniffSeconds = 2f;            // 스팟마다 킁킁
        [SerializeField, Range(0f, 1f)] private float _catDisturbChance = 0.3f; // 스팟을 건드려 안을 확인할 확률
        [SerializeField] private float _catPounceWindowSeconds = 0.5f;   // 발각 뒤 쥐가 튀어나갈 틈
        [SerializeField] private float _catMultiHideSniffMul = 1.5f;     // 여럿이 같이 숨으면 킁킁 시간 × 인원 × 이 값
        [SerializeField] private float _hideEnterSeconds = 0.3f;         // 들어가기 홀드

        [Header("호기심 앞발 (design/cat-ideas/03)")]
        [SerializeField] private float _catCuriosityMinSpeed = 1.5f;     // 이 속도 이상 움직이는 풀린 물건에 끌림
        [SerializeField] private float _catCuriousSpeed = 4f;
        [SerializeField] private Vector2Int _catPawCountRange = new(3, 5); // 앞발 횟수 (최소, 최대 포함)
        [SerializeField] private float _catPawIntervalSeconds = 0.6f;
        [SerializeField] private float _catPawImpulse = 1.2f;            // 앞발 한 번 속도 변화 (m/s, 질량 무관)
        [SerializeField] private float _catPawReach = 0.9f;              // 수평 거리 이 안이면 친다
        [SerializeField] private float _catCuriousYawnSeconds = 5f;      // 다 치고 하품
        [SerializeField] private float _catCuriousCooldownSeconds = 10f; // 같은 물건 다시 끌리기까지
        [SerializeField] private float _catBoredomSeconds = 30f;         // 같은 물건 누적 이만큼 놀면 질림
        [SerializeField] private float _catBoredIgnoreSeconds = 60f;     // 질린 물건 무시 시간
        [SerializeField] private float _catCuriousReachTimeout = 8f;     // 못 닿으면(선반 위 등) 포기

        [Header("기억·앙심 (design/cat-ideas/05)")]
        [SerializeField] private float _catMemoryCellSize = 2f;          // 장소 기억 격자 (m)
        [SerializeField] private float _catMemorySightPerSec = 1f;       // 쥐를 보는 동안 그 칸 열 +/s
        [SerializeField] private float _catMemoryNoiseMin = 40f;         // 이 이상 들린 소음만 기억 (+loudness/40)
        [SerializeField] private float _catMemoryDecayPerSec = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _catMemoryPatrolChance = 0.3f; // 순찰 목적지 고를 때 기억 칸으로 갈 확률
        [SerializeField] private float _catMemoryMinHeat = 3f;           // 이 열 이상인 칸만 찾아감
        [SerializeField] private float _catMemorySniffSeconds = 1.5f;
        [SerializeField] private float _catGrudgeEscape = 40f;           // 추격에서 놓치면 그 쥐 앙심 +
        [SerializeField] private float _catGrudgeSqueak = 15f;           // 들리는 곳에서 찍찍(도발) +
        [SerializeField] private float _catGrudgeDecayPerSec = 0.2f;
        [SerializeField] private float _catGrudgeThreshold = 50f;        // 이상이면 "찍힘"
        [SerializeField] private float _catGrudgeSightMul = 1.5f;        // 찍힌 쥐 시야 게인 배율
        [SerializeField] private float _catGrudgeHearingMul = 1.3f;      // 찍힌 쥐 발소리·찍찍 청각 배율
        [SerializeField] private float _catGrudgeGiveUpBonus = 2f;       // 성격 추격 포기 시간 + (찍힌 쥐)
        [SerializeField] private float _noiseAttributionSeconds = 3f;    // 놓은·던진 물건의 충돌·깨짐을 그 쥐 소리로 보는 시간
        [SerializeField] private float _catGrudgeBreak = 30f;            // 쥐 탓으로 깨진 소리 +
        [SerializeField] private float _catGrudgeFooled = 20f;           // 털실에 속은 뒤 던진 쥐 +
        [SerializeField] private float _catGrudgeBribe = 50f;            // 치즈 뇌물 -
        [SerializeField] private float _catBribeRadius = 1.5f;           // 고양이 정면 이 안에 내려놓은 치즈
        [SerializeField] private float _catBribeRecentSeconds = 10f;     // 이 안에 쥐가 내려놓은 것만 뇌물
        [SerializeField] private float _catBribeEatSeconds = 4f;

        [Header("냄새 (design/cat-ideas/06)")]
        [SerializeField] private float _scentIntervalMeters = 1.5f;      // 이만큼 걸을 때마다 자국 1개
        [SerializeField] private float _scentEdible = 60f;               // 치즈류를 들었을 때 자국 강도
        [SerializeField] private float _scentGrudge = 20f;               // 찍힌 쥐는 빈손이어도 약한 자국
        [SerializeField] private float _scentDecayPerSec = 3f;           // 60 → 20s
        [SerializeField] private int _scentMaxMarks = 32;
        [SerializeField] private float _scentCrouchIntervalMul = 2f;     // 웅크리면 간격 ×
        [SerializeField] private float _catScentDetectRadius = 3f;       // 순찰 중 이 반경 자국을 맡음
        [SerializeField] private float _catScentMinStrength = 8f;
        [SerializeField] private float _catTrackSpeed = 3f;
        [SerializeField] private float _catTrackSniffSeconds = 0.5f;     // 자국마다 킁킁
        [SerializeField] private float _puddleRadius = 2.5f;             // 엎은 물그릇 웅덩이 (design/cat-ideas/06 2단계)
        [SerializeField] private float _puddleSeconds = 60f;
        [Header("선반·점프 (design/cat-ideas/09·01·11, 고양이 39)")]
        [SerializeField] private float _catPerchSeconds = 25f;           // 선반 위에서 내려다보는 시간
        [SerializeField] private float _catPerchViewDistance = 12f;      // 선반 위 시야 거리 (평소 8)
        [SerializeField] private float _catJumpSeconds = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _catJumpFailChance = 0.15f; // 올라가는 점프 실패
        [SerializeField] private float _catJumpFailStunSeconds = 1.5f;
        [Header("문 (design/cat-ideas/09, 고양이 40)")]
        [SerializeField] private float _doorPushSeconds = 4f;            // 쫓던 고양이가 닫힌 문을 밀어 여는 시간
        [SerializeField] private float _doorPushRange = 1.4f;            // 문 중심에서 이 안이면 미는 중
        [SerializeField] private float _doorCloseNoise = 25f;            // 쥐가 문 닫는 소리
        [Header("귀 (design/cat-ideas/13, 고양이 42)")]
        [SerializeField, Range(0f, 1f)] private float _catEarHearMul = 0.5f; // 청각 임계의 이 배 이상 크기면 귀만 쫑긋
        [SerializeField] private float _catEarRadiusMul = 1.5f;          // 소리 전파 반경의 이 배까지 귀는 듣는다 (게이지는 반경 안만)
        [SerializeField] private float _catEarHoldSeconds = 2f;          // 소리 쪽을 이만큼 들음
        [Header("레이저 포인터 (design/cat-ideas/13, 고양이 41)")]
        [SerializeField] private float _laserBatterySeconds = 20f;       // 든 동안만 닳는다
        [SerializeField] private float _laserAimRange = 25f;
        [SerializeField] private float _laserAttractRadius = 12f;        // 점이 이 안이고 보이면 고양이가 쫓는다
        [SerializeField] private float _laserLingerSeconds = 5f;         // 점이 꺼져도 이만큼 두리번
        [SerializeField] private float _catLaserChaseSpeed = 4.5f;       // 점을 쫓는 속도
        [Header("흔들리는 끈 (design/cat-ideas/13, 고양이 38)")]
        [SerializeField] private float _stringSwingSeconds = 30f;
        [SerializeField] private float _stringAttractRadius = 8f;        // 이 안의 한가한 고양이가 보면 온다
        [SerializeField] private float _stringDistractSeconds = 10f;     // 앞발질 시간
        [SerializeField] private float _stringCatCooldown = 40f;         // 같은 고양이는 이만큼 다시 안 속음
        [SerializeField] private float _stringRetriggerRadius = 1.5f;    // 쥐가 이만큼 옆을 지나가면 다시 흔들림
        [Header("후추 (design/cat-ideas/06, 고양이 33)")]
        [SerializeField] private float _pepperSpillImpactSpeed = 4f;     // 던지지 않고 떨어뜨려도 이 속도 이상이면 쏟아짐
        [SerializeField] private float _pepperRadius = 2f;
        [SerializeField] private float _pepperSeconds = 45f;
        [SerializeField] private float _pepperSneezeSeconds = 2f;        // 고양이 재채기 정지
        [SerializeField] private float _pepperSneezeCooldown = 8f;       // 같은 고양이 재채기 간격
        [SerializeField] private float _wetSeconds = 20f;                // 웅덩이를 지난 쥐가 젖어 있는 시간
        [SerializeField] private float _scentWet = 40f;                  // 젖은 발자국 강도
        [SerializeField] private float _waterBowlSpillNoise = 30f;
        [SerializeField] private float _bedCoverRadius = 1.5f;           // 고양이 잠자리 이 안에 들어간 쥐는
        [SerializeField] private float _bedCoverSeconds = 30f;           // 이만큼 냄새 자국 없음 (고양이 냄새로 덮임)

        [Header("가지고 놀기 (design/cat-ideas/04)")]
        [SerializeField] private float _catToySeconds = 30f;             // 이 안에 못 벗어나면 진짜 다운
        [SerializeField] private float _catToyInterest = 100f;
        [SerializeField] private float _catToyLoopDrain = 35f;           // 툭툭 한 번 끝날 때마다 관심 -
        [SerializeField] private float _catToyDistractDrain = 40f;       // 즉시 조사 소음(깨짐·찍찍·함정) 관심 -
        [SerializeField] private float _catToyBatSeconds = 2f;
        [SerializeField] private float _catToyNudge = 0.4f;              // 앞발로 칠 때 잡힌 쥐가 밀리는 거리 (m)
        [SerializeField] private float _catToyReleaseSeconds = 3f;       // 놓아주는 창
        [SerializeField] private float _catToyEscapeDistance = 2f;       // 놓아준 자리에서 이만큼 벗어나면 도주 → 추격
        [SerializeField] private float _catToyYawnSeconds = 2f;
        [SerializeField] private float _catToyResumeWindow = 10f;        // 다시 잡으면 관심·남은 시간 이어서
        [SerializeField] private float _catToyBoredIgnoreSeconds = 6f;   // 질려서 놓아준 쥐는 하품 뒤 이만큼 못 본 척
        [SerializeField, Range(0f, 1f)] private float _catToyCarryChance = 0.6f; // 물고 옮기기 (고양이 34) — 놀이 한 판에 한 번, 첫 툭툭 뒤
        [SerializeField] private float _catToyCarrySeconds = 6f;
        [SerializeField] private float _catToyCarryMinDistance = 4f;     // 목적지(침대·햇볕) 거리 범위
        [SerializeField] private float _catToyCarryMaxDistance = 12f;
        [SerializeField] private float _catToyCarrySpeedMul = 1.2f;      // 순찰 속도 배율 — 자랑스럽게 종종
        [SerializeField, Range(0f, 1f)] private float _catToyCarryDropChance = 0.1f; // 초당 — 물고 가다 떨어뜨림
        [SerializeField] private float _catToyStruggleNudgeMul = 2f;     // 버둥 (고양이 35) — 툭 칠 때 보는 쪽으로 이 배수만큼
        [SerializeField] private int _catToyStrugglePressesPerDrain = 6; // 버둥 이만큼마다
        [SerializeField] private float _catToyStruggleDrain = 8f;        // 관심 -
        [SerializeField] private float _catToyStruggleMinInterval = 0.08f; // 버둥 최소 간격(초) — 매크로 연타 거름

        [Header("댕청한 실패 (design/cat-ideas/11)")]
        [SerializeField] private float _catSlipMinSpeed = 4f;            // 이 속도 이상으로 달릴 때만 미끄러진다
        [SerializeField] private float _catSlipDetectRadius = 0.7f;      // 발밑 Slippery 물건 판정
        [SerializeField] private float _catSlipDistance = 3f;
        [SerializeField] private float _catSlipSeconds = 0.6f;
        [SerializeField] private float _catSlipStunSeconds = 2f;         // 미끄러지다 벽에 박으면
        [SerializeField] private float _catSlipRecoverSeconds = 0.8f;    // 안 박으면 추스르기
        [SerializeField] private float _catSlipCooldown = 4f;
        [SerializeField] private float _catStartleRadius = 2.5f;         // 놀다가 이 안에서 깨짐·찍찍·함정 → 펄쩍
        [SerializeField] private float _catStartleSeconds = 1.5f;
        [SerializeField] private float _catWobbleSeconds = 10f;          // 캣닢 뒤 비틀거림
        [SerializeField] private float _catWobbleSpeedMul = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _catHairballChance = 0.2f; // 그루밍 끝나면 헤어볼 확률
        [SerializeField] private float _catHairballSeconds = 3f;         // 웩웩

        [Header("루틴 예고·화장실·물 (design/cat-ideas/02)")]
        [SerializeField] private float _catLitterSeconds = 12f;
        [SerializeField] private float _catLitterSense = 0.3f;
        [SerializeField] private float _catZoomiesSeconds = 10f;         // 화장실 뒤 우다다
        [SerializeField] private float _catZoomiesSpeed = 6.5f;          // 추격(5.5)보다 빠름 — 목적은 없다
        [SerializeField] private float _catZoomiesRadius = 8f;
        [SerializeField] private float _catZoomiesStep = 1.2f;           // 이 간격마다 새 방향
        [SerializeField] private float _catWaterSeconds = 8f;
        [SerializeField] private float _catCueHearRange = 18f;           // 루틴 예고가 들리는 거리

        [Header("매복·상자 (design/cat-ideas/09)")]
        [SerializeField] private float _catAmbushSeconds = 60f;
        [SerializeField] private float _catAmbushPounceRange = 2f;       // 이 안 쥐는 추격 없이 덮친다
        [SerializeField] private float _catAmbushCueInterval = 10f;      // "츄릅" 자막 간격
        [SerializeField] private float _catPounceSwingSeconds = 0.2f;    // 덮치기 스윙 (보통 0.4)
        [SerializeField] private Vector2 _catBoxSeconds = new(15f, 40f);
        [SerializeField] private float _catBoxViewDistance = 3f;         // 상자 안 — 정면 좁게
        [SerializeField] private float _catBoxViewHalfAngle = 45f;
        [SerializeField] private float _catBoxBlockRadius = 2f;          // 이 안 숨을 곳 입구를 막는다

        [Header("경계도 디렉터 (design/cat-ideas/12) — 고양이 감각 수치는 안 건드린다")]
        [SerializeField] private float _directorTickSeconds = 10f;
        [SerializeField] private float _tensionSuspicious = 10f;
        [SerializeField] private float _tensionChase = 25f;
        [SerializeField] private float _tensionToy = 20f;
        [SerializeField] private float _tensionDown = 40f;
        [SerializeField] private float _tensionNoise = 5f;
        [SerializeField] private float _tensionNoiseMin = 40f;
        [SerializeField] private float _tensionDecayPerSec = 1f;
        [SerializeField] private float _buildupThreshold = 25f;          // 긴장이 이 아래이고
        [SerializeField] private float _buildupQuietSeconds = 60f;       // 마지막 위기(추격·다운) 뒤 이만큼 → Build-up
        [SerializeField] private float _reliefThreshold = 70f;
        [SerializeField] private Vector2 _reliefSecondsRange = new(20f, 40f);
        [SerializeField] private float _buildupSleepMul = 0.7f;          // Build-up·Finale: 잠 짧게
        [SerializeField] private float _buildupDwellMul = 0.6f;          // Look 머무름 짧게 (촘촘한 순찰)
        [SerializeField] private float _buildupMemoryBonus = 0.3f;       // 기억 칸 방문 확률 +
        [SerializeField] private float _reliefRoutineWeightMul = 4f;     // Relief: Groom·Sun·Bed 스팟 가중치 ×
        [SerializeField] private float _reliefLookWeightMul = 0.3f;
        [SerializeField] private float _reliefDwellMul = 1.5f;           // 루틴 머무름 ×
        [SerializeField, Range(0f, 1f)] private float _finaleHoleBias = 0.6f; // Finale: 순찰 목적지를 쥐구멍 근처로
        [SerializeField] private float _finaleHoleRadius = 4f;

        [Header("집주인 이벤트 (design/cat-ideas/10)")]
        [SerializeField] private Vector2Int _houseEventsPerStage = new(2, 4);
        [SerializeField] private Vector2 _houseEventFirstDelay = new(90f, 150f);
        [SerializeField] private Vector2 _houseEventGap = new(180f, 300f);
        [SerializeField] private float _houseEventWarnSeconds = 3f;      // 예고음 → 본 사건
        [SerializeField] private Vector2 _catCallAwaySeconds = new(20f, 40f);
        [SerializeField] private float _catFeedingSeconds = 25f;
        [SerializeField] private float _catAwayWalkSpeed = 3f;
        [SerializeField] private float _reliefCallAwayMinGap = 90f;      // 디렉터 Relief면 마지막 사건 뒤 이만큼 지났을 때 부르기를 앞당김
        [SerializeField] private float _doorbellHoldSeconds = 1f;        // 초인종 E 홀드 (런당 1회)
        [SerializeField] private float _attentionIndicatorRange = 15f;   // 이 안에서 노는 고양이면 HUD "♪"
        [SerializeField] private float _vacuumSeconds = 30f;             // 로봇청소기 (집주인 이벤트)
        [SerializeField] private float _vacuumSpeed = 1.5f;
        [SerializeField] private float _vacuumMaskLoudness = 40f;        // 이 미만 소리는 청소기 소리에 묻힘
        [SerializeField] private float _vacuumCatSense = 0.6f;           // 피신한 고양이 감각
        [SerializeField] private float _tvSeconds = 90f;                 // TV (집주인 이벤트)
        [SerializeField] private float _tvMaskLoudness = 25f;            // TV 소리에 묻히는 loudness
        [SerializeField] private float _tvWatchDistance = 2.5f;          // 고양이가 TV 앞 이만큼에 앉는다
        [SerializeField] private float _lightOnSeconds = 30f;            // 불 켜짐 (집주인 이벤트) — 어둠 해제 시간
        [SerializeField] private float _lightOnGreetSeconds = 10f;       // 고양이가 집주인 다리에 부비는 시간
        [SerializeField] private float _lightOnGreetSense = 0.5f;        // 부비는 동안 감각
        [SerializeField] private float _windowSeconds = 20f;             // 창문 바람 (집주인 이벤트, 고양이 37)
        [SerializeField] private float _windowGustSpeed = 3f;            // 돌풍 한 번에 가벼운 물건에 주는 속도 (m/s) — 꾸준한 힘은 바닥 마찰(≈6 m/s²)을 못 이긴다
        [SerializeField] private float _windowGustInterval = 2f;         // 돌풍 간격 (s)
        [SerializeField] private float _windowMaxItemMass = 1f;          // 이 질량 이하만 바람에 굴러간다
        [SerializeField] private float _windowMaskLoudness = 20f;        // 바람 소리에 묻히는 loudness

        [Header("두 마리 — 앙숙 (design/cat-ideas/07)")]
        [SerializeField] private float _catFightDistance = 4f;           // 이 안에서 서로 보이면
        [SerializeField, Range(0f, 1f)] private float _catFightChance = 0.7f;
        [SerializeField] private float _catFightSeconds = 15f;
        [SerializeField] private float _catFightCooldown = 30f;          // 싸운 뒤 재발 금지
        [SerializeField] private float _catFightDeclineCooldown = 10f;   // 서로 무시하고 지나간 뒤 재판정 금지
        [SerializeField] private float _catFightViewMul = 0.2f;          // 싸우는 동안 시야 배율 (서로에게 꽂힘)
        [SerializeField, Range(0f, 1f)] private float _catBuddyChance = 0.5f; // 2마리일 때 짝꿍일 확률 (아니면 앙숙)
        [SerializeField] private float _catFlankRange = 20f;             // 짝꿍이 추격하면 이 안의 고양이가 협공
        [SerializeField] private float _catFlankLeadSeconds = 2f;        // 쥐의 이만큼 뒤 예상 위치로
        [SerializeField] private float _catFlankSeconds = 8f;
        [SerializeField] private float _catFlankSpeed = 4f;
        [SerializeField] private float _catBuddySleepMul = 1.5f;         // 짝꿍과 같이 자면 수면 ×
        [SerializeField] private float _catKittenCallCooldown = 10f;     // 아기 냐앙 → 엄마 호출 재사용 대기

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
        public float ThrowAimMinDistance => _throwAimMinDistance;
        public float ThrowAimMaxDistance => _throwAimMaxDistance;

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

        public float PingCooldownSeconds => _pingCooldownSeconds;
        public float PingMarkerSeconds => _pingMarkerSeconds;
        public float PingMaxDistance => _pingMaxDistance;

        /// <summary>파손 데미지: 문턱 초과분 × 0.15 (docs/05).</summary>
        public float GetFragileDamage(float impactSpeed) =>
            impactSpeed <= _fragileBreakSpeed ? 0f : (impactSpeed - _fragileBreakSpeed) * _fragileDamagePerSpeed;

        public float CatPatrolSpeed => _catPatrolSpeed;
        public float CatSuspiciousSpeed => _catSuspiciousSpeed;
        public float CatChaseSpeed => _catChaseSpeed;
        public float CatReturnSpeed => _catReturnSpeed;
        public float CatViewDistance => _catViewDistance;
        public float CatCrouchViewMultiplier => _catCrouchViewMultiplier;
        public float CatDarkViewMultiplier => _catDarkViewMultiplier;
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
        public float CatSuspiciousTravelSeconds => _catSuspiciousTravelSeconds;
        public float CatSleepSenseMultiplier => _catSleepSenseMultiplier;
        public Vector2 CatPatrolWaitRange => _catPatrolWaitRange;
        public float CatDistractedSpeed => _catDistractedSpeed;
        public float CatBedSleepSeconds => _catBedSleepSeconds;
        public float CatSpotFoodSeconds => _catSpotFoodSeconds;
        public float CatSpotFoodSense => _catSpotFoodSense;
        public float CatSpotSunSeconds => _catSpotSunSeconds;
        public float CatSpotSunSense => _catSpotSunSense;
        public float CatSpotGroomSeconds => _catSpotGroomSeconds;
        public float CatSpotGroomSense => _catSpotGroomSense;
        public int CatSpotAvoidRecent => _catSpotAvoidRecent;
        public Vector2 CatSleepLightRange => _catSleepLightRange;
        public Vector2 CatSleepDeepRange => _catSleepDeepRange;
        public float CatSleepForecastSeconds => _catSleepForecastSeconds;
        public float CatSleepLightSense => _catSleepLightSense;
        public float CatSleepDeepSense => _catSleepDeepSense;
        public float CatHalfAwakeSeconds => _catHalfAwakeSeconds;
        public float CatHalfAwakeSense => _catHalfAwakeSense;
        public float CatDeepWakeGaugeMul => _catDeepWakeGaugeMul;
        public float CatChaseLeadSeconds => _catChaseLeadSeconds;
        public float CatChaseOvershootMeters => _catChaseOvershootMeters;
        public float CatChaseCornerSpeedMul => _catChaseCornerSpeedMul;
        public float HideExitNoise => _hideExitNoise;
        public float CatSearchRadius => _catSearchRadius;
        public float CatSearchSeconds => _catSearchSeconds;
        public int CatSearchMaxSpots => _catSearchMaxSpots;
        public float CatSniffSeconds => _catSniffSeconds;
        public float CatDisturbChance => _catDisturbChance;
        public float CatPounceWindowSeconds => _catPounceWindowSeconds;
        public float CatMultiHideSniffMul => _catMultiHideSniffMul;
        public float HideEnterSeconds => _hideEnterSeconds;
        public float CatCuriosityMinSpeed => _catCuriosityMinSpeed;
        public float CatCuriousSpeed => _catCuriousSpeed;
        public Vector2Int CatPawCountRange => _catPawCountRange;
        public float CatPawIntervalSeconds => _catPawIntervalSeconds;
        public float CatPawImpulse => _catPawImpulse;
        public float CatPawReach => _catPawReach;
        public float CatCuriousYawnSeconds => _catCuriousYawnSeconds;
        public float CatCuriousCooldownSeconds => _catCuriousCooldownSeconds;
        public float CatBoredomSeconds => _catBoredomSeconds;
        public float CatBoredIgnoreSeconds => _catBoredIgnoreSeconds;
        public float CatCuriousReachTimeout => _catCuriousReachTimeout;
        public float CatMemoryCellSize => _catMemoryCellSize;
        public float CatMemorySightPerSec => _catMemorySightPerSec;
        public float CatMemoryNoiseMin => _catMemoryNoiseMin;
        public float CatMemoryDecayPerSec => _catMemoryDecayPerSec;
        public float CatMemoryPatrolChance => _catMemoryPatrolChance;
        public float CatMemoryMinHeat => _catMemoryMinHeat;
        public float CatMemorySniffSeconds => _catMemorySniffSeconds;
        public float CatGrudgeEscape => _catGrudgeEscape;
        public float CatGrudgeSqueak => _catGrudgeSqueak;
        public float CatGrudgeDecayPerSec => _catGrudgeDecayPerSec;
        public float CatGrudgeThreshold => _catGrudgeThreshold;
        public float CatGrudgeSightMul => _catGrudgeSightMul;
        public float CatGrudgeHearingMul => _catGrudgeHearingMul;
        public float CatGrudgeGiveUpBonus => _catGrudgeGiveUpBonus;
        public float NoiseAttributionSeconds => _noiseAttributionSeconds;
        public float CatGrudgeBreak => _catGrudgeBreak;
        public float CatGrudgeFooled => _catGrudgeFooled;
        public float CatGrudgeBribe => _catGrudgeBribe;
        public float CatBribeRadius => _catBribeRadius;
        public float CatBribeRecentSeconds => _catBribeRecentSeconds;
        public float CatBribeEatSeconds => _catBribeEatSeconds;
        public float ScentIntervalMeters => _scentIntervalMeters;
        public float ScentEdible => _scentEdible;
        public float ScentGrudge => _scentGrudge;
        public float ScentDecayPerSec => _scentDecayPerSec;
        public int ScentMaxMarks => _scentMaxMarks;
        public float ScentCrouchIntervalMul => _scentCrouchIntervalMul;
        public float CatScentDetectRadius => _catScentDetectRadius;
        public float CatScentMinStrength => _catScentMinStrength;
        public float CatTrackSpeed => _catTrackSpeed;
        public float CatTrackSniffSeconds => _catTrackSniffSeconds;
        public float PuddleRadius => _puddleRadius;
        public float PuddleSeconds => _puddleSeconds;
        public float CatPerchSeconds => _catPerchSeconds;
        public float CatPerchViewDistance => _catPerchViewDistance;
        public float CatJumpSeconds => _catJumpSeconds;
        public float CatJumpFailChance => _catJumpFailChance;
        public float CatJumpFailStunSeconds => _catJumpFailStunSeconds;
        public float DoorPushSeconds => _doorPushSeconds;
        public float DoorPushRange => _doorPushRange;
        public float DoorCloseNoise => _doorCloseNoise;
        public float CatEarHearMul => _catEarHearMul;
        public float CatEarRadiusMul => _catEarRadiusMul;
        public float CatEarHoldSeconds => _catEarHoldSeconds;
        public float LaserBatterySeconds => _laserBatterySeconds;
        public float LaserAimRange => _laserAimRange;
        public float LaserAttractRadius => _laserAttractRadius;
        public float LaserLingerSeconds => _laserLingerSeconds;
        public float CatLaserChaseSpeed => _catLaserChaseSpeed;
        public float StringSwingSeconds => _stringSwingSeconds;
        public float StringAttractRadius => _stringAttractRadius;
        public float StringDistractSeconds => _stringDistractSeconds;
        public float StringCatCooldown => _stringCatCooldown;
        public float StringRetriggerRadius => _stringRetriggerRadius;
        public float PepperSpillImpactSpeed => _pepperSpillImpactSpeed;
        public float PepperRadius => _pepperRadius;
        public float PepperSeconds => _pepperSeconds;
        public float PepperSneezeSeconds => _pepperSneezeSeconds;
        public float PepperSneezeCooldown => _pepperSneezeCooldown;
        public float WetSeconds => _wetSeconds;
        public float ScentWet => _scentWet;
        public float WaterBowlSpillNoise => _waterBowlSpillNoise;
        public float BedCoverRadius => _bedCoverRadius;
        public float BedCoverSeconds => _bedCoverSeconds;
        public float CatToyCarryChance => _catToyCarryChance;
        public float CatToyCarrySeconds => _catToyCarrySeconds;
        public float CatToyCarryMinDistance => _catToyCarryMinDistance;
        public float CatToyCarryMaxDistance => _catToyCarryMaxDistance;
        public float CatToyCarrySpeedMul => _catToyCarrySpeedMul;
        public float CatToyCarryDropChance => _catToyCarryDropChance;
        public float CatToyStruggleNudgeMul => _catToyStruggleNudgeMul;
        public int CatToyStrugglePressesPerDrain => _catToyStrugglePressesPerDrain;
        public float CatToyStruggleDrain => _catToyStruggleDrain;
        public float CatToyStruggleMinInterval => _catToyStruggleMinInterval;
        public float CatToySeconds => _catToySeconds;
        public float CatToyInterest => _catToyInterest;
        public float CatToyLoopDrain => _catToyLoopDrain;
        public float CatToyDistractDrain => _catToyDistractDrain;
        public float CatToyBatSeconds => _catToyBatSeconds;
        public float CatToyNudge => _catToyNudge;
        public float CatToyReleaseSeconds => _catToyReleaseSeconds;
        public float CatToyEscapeDistance => _catToyEscapeDistance;
        public float CatToyYawnSeconds => _catToyYawnSeconds;
        public float CatToyResumeWindow => _catToyResumeWindow;
        public float CatToyBoredIgnoreSeconds => _catToyBoredIgnoreSeconds;
        public float CatSlipMinSpeed => _catSlipMinSpeed;
        public float CatSlipDetectRadius => _catSlipDetectRadius;
        public float CatSlipDistance => _catSlipDistance;
        public float CatSlipSeconds => _catSlipSeconds;
        public float CatSlipStunSeconds => _catSlipStunSeconds;
        public float CatSlipRecoverSeconds => _catSlipRecoverSeconds;
        public float CatSlipCooldown => _catSlipCooldown;
        public float CatStartleRadius => _catStartleRadius;
        public float CatStartleSeconds => _catStartleSeconds;
        public float CatWobbleSeconds => _catWobbleSeconds;
        public float CatWobbleSpeedMul => _catWobbleSpeedMul;
        public float CatHairballChance => _catHairballChance;
        public float CatHairballSeconds => _catHairballSeconds;
        public float CatLitterSeconds => _catLitterSeconds;
        public float CatLitterSense => _catLitterSense;
        public float CatZoomiesSeconds => _catZoomiesSeconds;
        public float CatZoomiesSpeed => _catZoomiesSpeed;
        public float CatZoomiesRadius => _catZoomiesRadius;
        public float CatZoomiesStep => _catZoomiesStep;
        public float CatWaterSeconds => _catWaterSeconds;
        public float CatCueHearRange => _catCueHearRange;
        public float CatAmbushSeconds => _catAmbushSeconds;
        public float CatAmbushPounceRange => _catAmbushPounceRange;
        public float CatAmbushCueInterval => _catAmbushCueInterval;
        public float CatPounceSwingSeconds => _catPounceSwingSeconds;
        public Vector2 CatBoxSeconds => _catBoxSeconds;
        public float CatBoxViewDistance => _catBoxViewDistance;
        public float CatBoxViewHalfAngle => _catBoxViewHalfAngle;
        public float CatBoxBlockRadius => _catBoxBlockRadius;
        public float DirectorTickSeconds => _directorTickSeconds;
        public float TensionSuspicious => _tensionSuspicious;
        public float TensionChase => _tensionChase;
        public float TensionToy => _tensionToy;
        public float TensionDown => _tensionDown;
        public float TensionNoise => _tensionNoise;
        public float TensionNoiseMin => _tensionNoiseMin;
        public float TensionDecayPerSec => _tensionDecayPerSec;
        public float BuildupThreshold => _buildupThreshold;
        public float BuildupQuietSeconds => _buildupQuietSeconds;
        public float ReliefThreshold => _reliefThreshold;
        public Vector2 ReliefSecondsRange => _reliefSecondsRange;
        public float BuildupSleepMul => _buildupSleepMul;
        public float BuildupDwellMul => _buildupDwellMul;
        public float BuildupMemoryBonus => _buildupMemoryBonus;
        public float ReliefRoutineWeightMul => _reliefRoutineWeightMul;
        public float ReliefLookWeightMul => _reliefLookWeightMul;
        public float ReliefDwellMul => _reliefDwellMul;
        public float FinaleHoleBias => _finaleHoleBias;
        public float FinaleHoleRadius => _finaleHoleRadius;
        public Vector2Int HouseEventsPerStage => _houseEventsPerStage;
        public Vector2 HouseEventFirstDelay => _houseEventFirstDelay;
        public Vector2 HouseEventGap => _houseEventGap;
        public float HouseEventWarnSeconds => _houseEventWarnSeconds;
        public Vector2 CatCallAwaySeconds => _catCallAwaySeconds;
        public float CatFeedingSeconds => _catFeedingSeconds;
        public float CatAwayWalkSpeed => _catAwayWalkSpeed;
        public float ReliefCallAwayMinGap => _reliefCallAwayMinGap;
        public float DoorbellHoldSeconds => _doorbellHoldSeconds;
        public float AttentionIndicatorRange => _attentionIndicatorRange;
        public float VacuumSeconds => _vacuumSeconds;
        public float VacuumSpeed => _vacuumSpeed;
        public float VacuumMaskLoudness => _vacuumMaskLoudness;
        public float VacuumCatSense => _vacuumCatSense;
        public float TvSeconds => _tvSeconds;
        public float TvMaskLoudness => _tvMaskLoudness;
        public float TvWatchDistance => _tvWatchDistance;
        public float LightOnSeconds => _lightOnSeconds;
        public float LightOnGreetSeconds => _lightOnGreetSeconds;
        public float LightOnGreetSense => _lightOnGreetSense;
        public float WindowSeconds => _windowSeconds;
        public float WindowGustSpeed => _windowGustSpeed;
        public float WindowGustInterval => _windowGustInterval;
        public float WindowMaxItemMass => _windowMaxItemMass;
        public float WindowMaskLoudness => _windowMaskLoudness;
        public float CatFightDistance => _catFightDistance;
        public float CatFightChance => _catFightChance;
        public float CatFightSeconds => _catFightSeconds;
        public float CatFightCooldown => _catFightCooldown;
        public float CatFightDeclineCooldown => _catFightDeclineCooldown;
        public float CatFightViewMul => _catFightViewMul;
        public float CatBuddyChance => _catBuddyChance;
        public float CatFlankRange => _catFlankRange;
        public float CatFlankLeadSeconds => _catFlankLeadSeconds;
        public float CatFlankSeconds => _catFlankSeconds;
        public float CatFlankSpeed => _catFlankSpeed;
        public float CatBuddySleepMul => _catBuddySleepMul;
        public float CatKittenCallCooldown => _catKittenCallCooldown;
        public float LureYarnSeconds => _lureYarnSeconds;
        public float LureYarnRadius => _lureYarnRadius;
        public float LureCatnipSeconds => _lureCatnipSeconds;
        public float LureCatnipRadius => _lureCatnipRadius;

        public float StunSeconds => _stunSeconds;

        public int BaseCarrySlots => _baseCarrySlots;
        public float UpgradeMoveSpeedPerLevel => _upgradeMoveSpeedPerLevel;
        public float UpgradeStaminaPerLevel => _upgradeStaminaPerLevel;
        public float UpgradeThrowPerLevel => _upgradeThrowPerLevel;
        public float UpgradeJumpPerLevel => _upgradeJumpPerLevel;
        public float HighFallStunHeight => _highFallStunHeight;
        public float RescueHoldSeconds => _rescueHoldSeconds;
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
    }
}
