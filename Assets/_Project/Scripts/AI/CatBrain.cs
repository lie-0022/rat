using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    public enum CatState { Sleep, Patrol, Suspicious, Chase, Capture, Distracted, Return, Search /* 숨을 곳 수색 (2026-09-24, CatBrain.Search.cs) */, Curious /* 호기심 앞발 (2026-09-24, CatBrain.Curious.cs) */, Track /* 냄새 추적 (2026-09-24, CatBrain.Track.cs) */, Toy /* 가지고 놀기 (2026-09-24, CatBrain.Toy.cs) */, Blunder /* 댕청한 실패 (2026-09-24, CatBrain.Blunder.cs) */, Away /* 집주인이 불러 나감 (2026-09-24, CatBrain.House.cs) */, Fight /* 앙숙 싸움 (2026-09-24, CatBrain.Fight.cs) */, Zoomies /* 화장실 뒤 우다다 (2026-09-24, CatBrain.Routine.cs) */, Ambush /* 매복 (2026-09-24, CatBrain.Lurk.cs) */, BoxSit /* 상자 입구에 앉음 */, Flank /* 짝꿍 협공 (2026-09-24, CatBrain.Buddy.cs) */, Respond /* 엄마가 아기에게 달려감 */ }

    /// <summary>잠의 단계 (design/cat-ideas/08). 클라 연출용으로 복제 — 꼬리·숨소리로 읽힌다.</summary>
    public enum CatSleepPhase : byte { None, Light, ToDeep, Deep, ToLight, HalfAwake }

    /// <summary>
    /// 고양이 FSM (docs/07, 호스트에서만 Update). 클라는 NetworkTransform 보간 + 상태 연출만.
    /// Distracted(유인 아이템)는 태스크 1-6에서 트리거가 생긴다 — 진입 API만 준비.
    /// 애니·사운드 연출 계약(CatAnimatorLink)은 아트 단계.
    /// </summary>
    public partial class CatBrain : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private CatPersonalitySO[] _personalities;   // 스폰 시 하나 뽑음 (비어 있으면 기본 배율 1)

        public NetworkVariable<CatState> State = new NetworkVariable<CatState>(CatState.Patrol);
        public NetworkVariable<ulong> TargetClientId = new NetworkVariable<ulong>(0);
        public NetworkVariable<CatSleepPhase> SleepPhase = new NetworkVariable<CatSleepPhase>(CatSleepPhase.None);
        /// <summary>뽑힌 성격 인덱스(-1 없음). 클라 연출용.</summary>
        public NetworkVariable<sbyte> PersonalityIndex = new NetworkVariable<sbyte>(-1);

        public CatPersonalitySO Personality =>
            _personalities != null && PersonalityIndex.Value >= 0 && PersonalityIndex.Value < _personalities.Length
                ? _personalities[PersonalityIndex.Value] : null;
        private float ViewMul => Personality != null ? Personality.ViewMultiplier : 1f;
        private float HearingMul => Personality != null ? Personality.HearingMultiplier : 1f;
        private float ChaseSpeedMul => Personality != null ? Personality.ChaseSpeedMultiplier : 1f;
        private float SleepDurMul => Personality != null ? Personality.SleepDurationMultiplier : 1f;
        private float LookDwellMul => Personality != null ? Personality.LookDwellMultiplier : 1f;
        private float ChaseGiveUp => Personality != null ? Personality.ChaseGiveUpSeconds : 0f;
        private float _chaseStartedAt;

        private CatSenses _senses;
        private CatMovement _movement;

        // 스팟 그래프 (design/cat-design/02): CatSpot 컴포넌트가 없으면 씬의 CatWaypoint* 를 Look 스팟으로 쓴다
        private struct Spot { public Vector3 Pos; public CatSpotType Type; public float Weight; public string Name; }
        private Spot[] _spots;
        private int _spotIndex = -1;
        private readonly System.Collections.Generic.List<int> _recentSpots = new();
        private bool _dwelling;                 // 스팟에 도착해 머무는 중
        private float _waitUntil;               // Patrol 대기(스팟 머무름 끝)
        private float _sleepUntil;              // Bed 잠 끝
        private float _phaseUntil;              // 잠 단계 끝
        private CatSleepPhase _phaseAfterHalfAwake;
        public CatSpotType CurrentSpotType => _spotIndex >= 0 && _spots != null ? _spots[_spotIndex].Type : CatSpotType.Look;
        public string CurrentSpotName => _spotIndex >= 0 && _spots != null ? _spots[_spotIndex].Name : "";
        private Vector3 _investigatePos;
        private float _suspiciousUntil;
        private bool _suspiciousArrived; // 조사 지점에 닿았나 — 배회 6s는 닿은 뒤부터 (docs/07)
        private PlayerCondition _chaseTarget;
        private Vector3 _lastKnownTargetPos; // 시야 있을 때만 갱신 — 월핵 추적 금지 (docs/07)
        private Vector3 _targetVelocity;     // 위치 차분 (원격 플레이어는 kinematic이라 Rigidbody 속도가 0)
        private Vector3 _targetPrevPos;
        private bool _overshooting;          // 시야 상실 직후 진행 방향으로 더 가 보는 중
        private float _lostSightSince = -1f;
        private float _captureSwingEnd;
        private float _groomUntil;
        private Vector3 _distractPos;
        private float _distractUntil;
        private CatState _stateBeforeDistract;

        private void Awake()
        {
            _senses = GetComponent<CatSenses>();
            _movement = GetComponent<CatMovement>();
        }

        public override void OnNetworkSpawn()
        {
            InitBellView(); // 모든 클라 — 방울 (고양이 140)
            if (!IsServer)
            {
                enabled = false;
                _movement.SetEnabled(false);
                return;
            }
            _movement.SetEnabled(true); // 프리팹은 꺼 둠 — 클라에서 NavMesh 없이 켜지며 오류가 나서 (고양이 127)
            CollectSpots();
            CollectHideSpots();
            _senses.CuriosityFilter = IsCuriosityAllowed;
            InitMemory();
            InitJump(); // 선반 점프 (고양이 39)
            InitEars(); // 귀 돌리기 (고양이 42)
            _senses.Heard += OnHeardForPersonality;
            Noise.ScentSystem.Configure(_balance);
            Noise.ScentSystem.Clear(); // 정적 버퍼 — 이전 판·이전 플레이 모드 자국 제거
            Noise.NoiseSystem.ClearMasks(); // 정적 — 이전 판 청소기·TV가 켜진 채 끝났을 수 있다
            if (PersonalityIndex.Value < 0 && _personalities != null && _personalities.Length > 0)
                ServerSetPersonality(PickRandomPersonality()); // 아기는 무작위에서 뺀다 (존 생성기가 명시)
            else ApplyPersonality();
            SetState(CatState.Patrol);
        }

        /// <summary>깨워서 순찰로 (집주인 부르기·존 시작·테스트용). 자는 중이면 다음 스팟을 바로 고른다. 호스트 전용.</summary>
        public void ServerWake()
        {
            if (!IsServer || State.Value == CatState.Chase || State.Value == CatState.Capture || State.Value == CatState.Toy) return;
            SetState(CatState.Patrol);
            _senses.ConsumeStimulus();
            GoToNextSpot();
        }

        /// <summary>호스트: 아기 고양이로 (존 생성기 — 엄마·아기 관계는 CatRelation이 알아서). 아기 성격이 없으면 false.</summary>
        public bool ServerMakeKitten()
        {
            if (!IsServer || _personalities == null) return false;
            for (int i = 0; i < _personalities.Length; i++)
                if (_personalities[i] != null && _personalities[i].IsKitten) { ServerSetPersonality(i); return true; }
            return false;
        }

        /// <summary>성격 지정 (스폰 시 랜덤, 테스트·존 생성기가 명시 지정 가능). 호스트 전용.</summary>
        public void ServerSetPersonality(int index)
        {
            if (!IsServer) return;
            PersonalityIndex.Value = (sbyte)Mathf.Clamp(index, -1, (_personalities?.Length ?? 0) - 1);
            ApplyPersonality();
            Log.Dev($"고양이 [{name}]: 성격 {(Personality != null ? Personality.DisplayName : "기본")}");
        }

        private void ApplyPersonality()
        {
            _senses.ViewMultiplier = ViewMul;
            _senses.HearingMultiplier = HearingMul;
            _senses.CuriositySpeedMultiplier = Personality != null ? Personality.CuriositySpeedMultiplier : 1f;
            _movement.SpeedMultiplier = (Personality != null ? Personality.MoveSpeedMultiplier : 1f) * _stageSpeedMul; // 스테이지 조건(간식 날) 곱 (고양이 106)
        }

        // 씬의 CatSpot 전부 (방 모듈 단계에서는 존 그래프로 — 지금은 씬 = 방 1개). 없으면 CatWaypoint* 이름을 Look으로
        private void CollectSpots()
        {
            var list = new System.Collections.Generic.List<Spot>();
            foreach (var spot in GameObject.FindObjectsByType<CatSpot>(FindObjectsSortMode.None))
                list.Add(new Spot { Pos = spot.transform.position, Type = spot.Type, Weight = spot.Weight, Name = spot.name });
            if (list.Count == 0)
                foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.StartsWith("CatWaypoint"))
                        list.Add(new Spot { Pos = t.position, Type = CatSpotType.Look, Weight = 1f, Name = t.name });
            _spots = list.ToArray();
            Log.Dev($"고양이 [{name}]: 스팟 {_spots.Length}개");
        }

        private Vector3? _tempWaypoint; // 쿠키 부스러기 — 1회 경유 (docs/07)

        /// <summary>쿠키 부스러기: 순찰 경로에 임시 삽입, 1회 경유 (docs/07·08).</summary>
        public void ServerInsertWaypoint(Vector3 pos)
        {
            if (!IsServer) return;
            _tempWaypoint = pos;
            Log.Dev($"고양이 [{name}]: 임시 웨이포인트 삽입 {pos:F1}");
        }

        /// <summary>유인 아이템 진입점 (docs/07 — Chase보다 우선순위 낮음). 태스크 1-6에서 호출.</summary>
        public void ServerDistract(Vector3 pos, float seconds, AttentionKind kind, bool wobbleAfter = false, ulong? fooledBy = null)
        {
            if (!IsServer || State.Value == CatState.Chase || State.Value == CatState.Capture || State.Value == CatState.Toy) return;
            // 찍힌 쥐가 던진 유인엔 안 속는다 — "네가 던진 거잖아" (design/cat-ideas/05)
            if (fooledBy.HasValue && IsGrudged(fooledBy.Value)) { Log.Dev($"고양이 [{name}]: client {fooledBy.Value}의 유인엔 안 속음"); return; }
            if (!AttentionWins(kind, out float score)) return; // 더 센 관심에 꽂혀 있음 (고양이 261)
            seconds *= BoredomScale(kind);
            _attentionScore = score;
            _fooledBy = fooledBy;
            _distractPos = pos;
            _wobbleAfterDistract = wobbleAfter; // 캣닢 — 끝나면 비틀거림 (design/cat-ideas/11)
            _distractUntil = Time.time + seconds;
            _stateBeforeDistract = State.Value == CatState.Distracted ? _stateBeforeDistract : State.Value;
            SetState(CatState.Distracted);
        }

        private void Update()
        {
            if (!IsSpawned || _spots == null) return; // 자동 부트 등 스폰 전 프레임 가드
            TickMemory();
            CheckSlip(); // 달리다 비누를 밟으면 어떤 상태든 미끄러진다 (design/cat-ideas/11)
            CheckPepper(); // 후추 패치 — 재채기 (고양이 33)
            TickEars(); // 귀가 관심 쪽으로 (고양이 42)
            TickDirectorHints();
            TickHousePending();
            TickBedCover();
            switch (State.Value)
            {
                case CatState.Sleep: TickSleep(); break;
                case CatState.Patrol: TickPatrol(); break;
                case CatState.Suspicious: TickSuspicious(); break;
                case CatState.Chase: TickChase(); break;
                case CatState.Capture: TickCapture(); break;
                case CatState.Distracted: TickDistracted(); break;
                case CatState.Return: TickReturn(); break;
                case CatState.Search: TickSearch(); break;
                case CatState.Curious: TickCurious(); break;
                case CatState.Track: TickTrack(); break;
                case CatState.Toy: TickToy(); break;
                case CatState.Blunder: TickBlunder(); break;
                case CatState.Away: TickAway(); break;
                case CatState.Fight: TickFight(); break;
                case CatState.Zoomies: TickZoomies(); break;
                case CatState.Ambush:
                case CatState.BoxSit: TickLurk(); break;
                case CatState.Flank: TickFlank(); break;
                case CatState.Respond: TickRespond(); break;
            }
        }

        private void SetState(CatState next)
        {
            if (State.Value == next) return;
            Log.Dev($"고양이 [{name}]: {State.Value} → {next}");
            if (State.Value == CatState.Toy) ExitToy(); // 잡힌 쥐를 Pinned로 남기지 않게
            if (State.Value == CatState.Blunder) ExitBlunder();
            if (State.Value == CatState.Away) ExitAway();
            if (State.Value == CatState.Fight) ExitFight();
            if (State.Value == CatState.Ambush || State.Value == CatState.BoxSit) ExitLurk();
            var prevState = State.Value;
            State.Value = next;
            RaiseStateChanged(prevState, next); // 경계도 디렉터 긴장 입력
            _senses.SensitivityMultiplier = next == CatState.Sleep ? _balance.CatSleepSenseMultiplier : 1f;
            if (next != CatState.Patrol) { _dwelling = false; _goSit = false; ExitPerch(); }
            if (next != CatState.Sleep) SleepPhase.Value = CatSleepPhase.None;
            if (next != CatState.Curious) ExitCurious();
            if (next != CatState.Patrol) CancelMemoryVisit();

            switch (next)
            {
                case CatState.Sleep:
                    _movement.Stop();
                    _sleepUntil = Time.time + _balance.CatBedSleepSeconds * SleepDurMul * DirSleepMul * BuddySleepMul(); // 디렉터 Build-up이면 짧게, 짝꿍과 같이면 길게
                    EnterSleepPhase(CatSleepPhase.Light);
                    RoleNapCue(); // 문지기·순찰꾼이 졸면 근처 쥐에게 — 지나갈 틈 (고양이 101)
                    break;
                case CatState.Patrol: _waitUntil = 0f; _dwelling = false; break;
                case CatState.Suspicious:
                    _suspiciousArrived = false;
                    _suspiciousUntil = Time.time + _balance.CatSuspiciousTravelSeconds; // 가는 길 상한 — 배회 시간은 도착해서
                    _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                    break;
                case CatState.Chase:
                    _chaseStartedAt = Time.time;
                    _targetPrevPos = Vector3.zero;
                    _targetVelocity = Vector3.zero;
                    _overshooting = false;
                    TargetClientId.Value = _chaseTarget != null ? _chaseTarget.OwnerClientId : 0;
                    if (_chaseTarget != null) _lastKnownTargetPos = _chaseTarget.transform.position;
                    _lostSightSince = -1f;
                    break;
                case CatState.Capture:
                    _movement.Stop();
                    _captureSwingEnd = Time.time + _balance.CatCaptureSwingSeconds;
                    ApplyPounceSwing(); // 매복·상자 덮치기는 스윙이 짧다
                    _groomUntil = 0f;
                    break;
                case CatState.Return:
                    TargetClientId.Value = 0;
                    if (_spots.Length > 0)
                        _movement.MoveTo(NearestSpot(), _balance.CatReturnSpeed);
                    break;
                case CatState.Distracted:
                    _movement.MoveTo(_distractPos, _balance.CatDistractedSpeed);
                    break;
                case CatState.Search:
                    EnterSearchState();
                    break;
                case CatState.Curious:
                    EnterCuriousState();
                    break;
                case CatState.Track:
                    EnterTrackState();
                    break;
                case CatState.Toy:
                    EnterToyState();
                    break;
                case CatState.Away:
                    EnterAwayState();
                    break;
                case CatState.Fight:
                    EnterFightState();
                    break;
                case CatState.Zoomies:
                    EnterZoomiesState();
                    break;
                case CatState.Ambush:
                    EnterAmbushState();
                    break;
                case CatState.BoxSit:
                    EnterBoxSitState();
                    break;
            }
        }

        // ---- 상태별 틱 ---- 잠 CatBrain.Sleep · 순찰 CatBrain.Patrol · 추격·포획 CatBrain.Chase · 의심·유인·복귀·공통 전이 CatBrain.Alert (고양이 203)
    }
}
