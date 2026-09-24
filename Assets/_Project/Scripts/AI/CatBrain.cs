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
            if (!IsServer)
            {
                enabled = false;
                _movement.SetEnabled(false);
                return;
            }
            CollectSpots();
            CollectHideSpots();
            _senses.CuriosityFilter = IsCuriosityAllowed;
            InitMemory();
            _senses.Heard += OnHeardForPersonality;
            Noise.ScentSystem.Configure(_balance);
            Noise.ScentSystem.Clear(); // 정적 버퍼 — 이전 판·이전 플레이 모드 자국 제거
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
            _movement.SpeedMultiplier = Personality != null ? Personality.MoveSpeedMultiplier : 1f;
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
        public void ServerDistract(Vector3 pos, float seconds, bool wobbleAfter = false, ulong? fooledBy = null)
        {
            if (!IsServer || State.Value == CatState.Chase || State.Value == CatState.Capture || State.Value == CatState.Toy) return;
            // 찍힌 쥐가 던진 유인엔 안 속는다 — "네가 던진 거잖아" (design/cat-ideas/05)
            if (fooledBy.HasValue && IsGrudged(fooledBy.Value)) { Log.Dev($"고양이 [{name}]: client {fooledBy.Value}의 유인엔 안 속음"); return; }
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
            TickDirectorHints();
            TickHousePending();
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
            if (next != CatState.Patrol) _dwelling = false;
            if (next != CatState.Sleep) SleepPhase.Value = CatSleepPhase.None;
            if (next != CatState.Curious) ExitCurious();
            if (next != CatState.Patrol) CancelMemoryVisit();

            switch (next)
            {
                case CatState.Sleep:
                    _movement.Stop();
                    _sleepUntil = Time.time + _balance.CatBedSleepSeconds * SleepDurMul * DirSleepMul * BuddySleepMul(); // 디렉터 Build-up이면 짧게, 짝꿍과 같이면 길게
                    EnterSleepPhase(CatSleepPhase.Light);
                    break;
                case CatState.Patrol: _waitUntil = 0f; _dwelling = false; break;
                case CatState.Suspicious:
                    _suspiciousUntil = Time.time + _balance.CatSuspiciousWanderSeconds;
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

        // ---- 상태별 틱 ----

        // 얕은 잠 ↔ 깊은 잠 파동. 얕은 잠에서 자극 → 한쪽 눈 뜸(HalfAwake) → 더 자극 있으면 깸, 없으면 다시 잔다.
        // 깊은 잠은 임계 ×2(접시 깨짐 수준)만 HalfAwake로. 전환 3s 전 예고 단계(꼬리 씰룩)가 있어 쥐가 읽을 수 있다.
        private void TickSleep()
        {
            var phase = SleepPhase.Value;
            bool deep = phase == CatSleepPhase.Deep || phase == CatSleepPhase.ToLight;
            float threshold = _balance.CatSuspicionThreshold * (deep ? _balance.CatDeepWakeGaugeMul : 1f);
            // 게이지가 임계를 넘거나, 접시 깨짐·함정·찍찍(즉시 조사 소음)이면 깬다. 그 밖의 작은 자극은 자는 동안 잊는다 — 남겨 두면 깬 뒤 엉뚱한 곳을 조사하러 간다
            bool stimulated = _senses.HasNewStimulus && (_senses.SuspicionGauge.Value >= threshold || _senses.ImmediateInvestigate);
            if (_senses.HasNewStimulus && !stimulated) _senses.ConsumeStimulus();

            if (phase == CatSleepPhase.HalfAwake)
            {
                // 눈을 뜬 사이 보거나 새 자극 → 깸
                if (_senses.VisibleTarget != null || stimulated) { EnterSuspicious(); return; }
                if (Time.time >= _phaseUntil) EnterSleepPhase(_phaseAfterHalfAwake);
                return;
            }
            if (stimulated)
            {
                _senses.ConsumeStimulus();
                _phaseAfterHalfAwake = phase == CatSleepPhase.Deep || phase == CatSleepPhase.ToLight ? CatSleepPhase.Light : phase;
                EnterSleepPhase(CatSleepPhase.HalfAwake);
                return;
            }
            if (Time.time >= _sleepUntil && phase == CatSleepPhase.Light)
            {
                // 다 잤다 (깊은 잠 중엔 안 깬다 — 얕은 잠으로 돌아온 뒤). 아직 잠자리 위라 Arrived가 참이므로 다음 스팟을 바로 고른다
                SetState(CatState.Patrol);
                GoToNextSpot();
                return;
            }
            if (Time.time < _phaseUntil) return;
            switch (phase)
            {
                case CatSleepPhase.Light: EnterSleepPhase(CatSleepPhase.ToDeep); break;
                case CatSleepPhase.ToDeep: EnterSleepPhase(CatSleepPhase.Deep); break;
                case CatSleepPhase.Deep: EnterSleepPhase(CatSleepPhase.ToLight); break;
                case CatSleepPhase.ToLight: EnterSleepPhase(CatSleepPhase.Light); break;
            }
        }

        private void EnterSleepPhase(CatSleepPhase phase)
        {
            SleepPhase.Value = phase;
            float sense;
            float duration;
            switch (phase)
            {
                case CatSleepPhase.Light: sense = _balance.CatSleepLightSense; duration = Random.Range(_balance.CatSleepLightRange.x, _balance.CatSleepLightRange.y); break;
                case CatSleepPhase.ToDeep: sense = _balance.CatSleepLightSense; duration = _balance.CatSleepForecastSeconds; break;
                case CatSleepPhase.Deep: sense = _balance.CatSleepDeepSense; duration = Random.Range(_balance.CatSleepDeepRange.x, _balance.CatSleepDeepRange.y); break;
                case CatSleepPhase.ToLight: sense = _balance.CatSleepDeepSense; duration = _balance.CatSleepForecastSeconds; break;
                default: sense = _balance.CatHalfAwakeSense; duration = _balance.CatHalfAwakeSeconds; break;
            }
            _senses.SensitivityMultiplier = sense;
            _phaseUntil = Time.time + duration;
            Log.Dev($"고양이 [{name}]: 잠 {phase} ({duration:0.0}s, 감각 {sense})");
        }

        private void TickPatrol()
        {
            if (CheckEscalation()) return;
            if (TryEatBribe()) return;    // 쥐가 바친 치즈 (design/cat-ideas/05)
            if (CheckScent()) return;     // 냄새 자국 (의심 아래, 호기심 위 — design/cat-ideas/06)
            if (CheckCuriosity()) return; // 순찰 중엔 굴러가는 물건에 속는다 (의심·추격 중엔 안 속음)

            // 임시 웨이포인트(쿠키) 우선 — 도착하면 소비
            if (_tempWaypoint.HasValue)
            {
                _movement.MoveTo(_tempWaypoint.Value, _balance.CatPatrolSpeed);
                if (Vector3.Distance(transform.position, _tempWaypoint.Value) < 1f)
                {
                    _tempWaypoint = null;
                    _waitUntil = Time.time + Random.Range(_balance.CatPatrolWaitRange.x, _balance.CatPatrolWaitRange.y);
                }
                return;
            }

            if (_memoryVisit) { TickMemoryVisit(); return; }
            if (_spots.Length == 0) { _movement.Stop(); return; }
            if (_dwelling)
            {
                if (Time.time < _waitUntil) return;
                _dwelling = false;
                _senses.SensitivityMultiplier = 1f;
                if (CurrentSpotType == CatSpotType.Groom && TryHairball()) return; // 그루밍 끝 — 가끔 헤어볼 (design/cat-ideas/11)
                if (TryStartZoomies()) return; // 화장실 끝 — 우다다 (design/cat-ideas/02)
                GoToNextSpot();
                return;
            }
            if (_spotIndex < 0) { GoToNextSpot(); return; }
            if (_movement.Arrived) ArriveAtSpot();
            else if (_movement.Velocity.sqrMagnitude < 0.01f) _movement.MoveTo(_spots[_spotIndex].Pos, _balance.CatPatrolSpeed);
        }

        // 다음 스팟: 가중치 랜덤, 최근 n개 제외 (스팟이 적으면 제외 목록을 줄인다)
        private void GoToNextSpot()
        {
            if (IsFeeding) { int food = FindSpot(CatSpotType.Food); if (food >= 0) { _spotIndex = food; _movement.MoveTo(_spots[food].Pos, _balance.CatPatrolSpeed); return; } }
            if (TryFinaleHoleVisit()) return; // 귀환 카운트다운 — 쥐구멍 쪽 (design/cat-ideas/12)
            if (TryGoToMemorySpot()) return; // 가끔 기억 칸에 들른다 (design/cat-ideas/05)
            int avoid = Mathf.Min(_balance.CatSpotAvoidRecent, _spots.Length - 1);
            while (_recentSpots.Count > avoid) _recentSpots.RemoveAt(0);
            float total = 0f;
            for (int i = 0; i < _spots.Length; i++) if (!_recentSpots.Contains(i)) total += SpotWeight(i);
            if (total <= 0f) { _movement.Stop(); return; } // 순찰할 스팟이 없음 (문뿐)
            float r = Random.value * total;
            int pick = -1;
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_recentSpots.Contains(i) || SpotWeight(i) <= 0f) continue;
                r -= SpotWeight(i);
                if (r <= 0f) { pick = i; break; }
            }
            if (pick < 0) pick = 0;
            _spotIndex = pick;
            _recentSpots.Add(pick);
            _movement.MoveTo(_spots[pick].Pos, _balance.CatPatrolSpeed);
            AnnounceNextSpot(_spots[pick].Type); // 루틴 예고 — 소리로 읽힌다 (design/cat-ideas/02)
        }

        // 성격이 잠자리·관찰점 선호를 바꾼다 (design/cat-ideas/01)
        private float SpotWeight(int i)
        {
            if (_spots[i].Type == CatSpotType.Door) return 0f; // 문은 순찰 대상 아님
            float w = _spots[i].Weight * DirSpotWeightMul(_spots[i].Type); // 디렉터 Relief면 루틴 스팟 쪽으로
            var p = Personality;
            if (p == null) return w;
            return _spots[i].Type switch
            {
                CatSpotType.Bed => w * p.BedWeightMultiplier,
                CatSpotType.Look => w * p.LookWeightMultiplier,
                _ => w
            };
        }

        // 스팟 종류별 머무름: 잠자리는 Sleep 상태로, 나머지는 시간+감각 배율 (design/cat-design/02 카탈로그)
        private void ArriveAtSpot()
        {
            var spot = _spots[_spotIndex];
            // 복귀(NearestSpot)로 도착한 스팟도 "방금 쓴 스팟"으로 — 안 그러면 복귀 → 같은 매복 스팟 → 또 매복이 반복된다 (고양이 21 소크)
            if (!_recentSpots.Contains(_spotIndex)) _recentSpots.Add(_spotIndex);
            Log.Dev($"고양이 [{name}]: 스팟 도착 {spot.Name} ({spot.Type})");
            switch (spot.Type)
            {
                case CatSpotType.Bed:
                    SetState(CatState.Sleep);
                    return;
                case CatSpotType.Ambush:
                    EnterAmbush(); return;   // 커튼 뒤 매복 (design/cat-ideas/09)
                case CatSpotType.Box:
                    EnterBoxSit(); return;   // 상자 입구에 앉기
                case CatSpotType.Food:
                    // 밥 시간이면 남은 시간만큼 먹는다 (집주인 이벤트)
                    Dwell(IsFeeding ? _feedingUntil - Time.time : _balance.CatSpotFoodSeconds * DirRoutineDwellMul, _balance.CatSpotFoodSense); return;
                case CatSpotType.Sun:
                    Dwell(_balance.CatSpotSunSeconds * DirRoutineDwellMul, _balance.CatSpotSunSense); return;
                case CatSpotType.Groom:
                    Dwell(_balance.CatSpotGroomSeconds * DirRoutineDwellMul, _balance.CatSpotGroomSense); return;
                case CatSpotType.Litter:
                    _litterDone = true; // 볼일 끝나면 우다다
                    Dwell(_balance.CatLitterSeconds, _balance.CatLitterSense); return;
                case CatSpotType.Water:
                    Dwell(_balance.CatWaterSeconds, 1f); return;
                default:
                    Dwell(Random.Range(_balance.CatPatrolWaitRange.x, _balance.CatPatrolWaitRange.y) * LookDwellMul * DirLookDwellMul, 1f); return;
            }
        }

        private void Dwell(float seconds, float sense)
        {
            _dwelling = true;
            _waitUntil = Time.time + seconds;
            _senses.SensitivityMultiplier = sense;
            _movement.Stop();
        }

        private void TickSuspicious()
        {
            if (CheckEscalation()) return;
            if (TryEatBribe()) return;
            if (Personality != null && Personality.CuriousWhileSuspicious && CheckCuriosity()) return; // 호기심쟁이는 의심 중에도 속는다

            // 새 자극이 오면 조사 지점 갱신 + 시간 연장
            if (_senses.HasNewStimulus)
            {
                _investigatePos = _senses.LastStimulusPos;
                _suspiciousUntil = Time.time + _balance.CatSuspiciousWanderSeconds;
                _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                _senses.ConsumeStimulus();
            }
            if (_movement.Arrived) // 주변 3m 배회 (docs/07)
                _movement.MoveTo(_movement.RandomPointAround(_investigatePos, 3f), _balance.CatSuspiciousSpeed);

            if (_senses.SuspicionGauge.Value <= 0f || Time.time >= _suspiciousUntil)
                SetState(CatState.Return);
        }

        private void TickChase()
        {
            // 숨은(Hidden) 타깃은 "놓친 것"으로 다룬다 — 시야 상실 분기를 거쳐 수색으로 (design/cat-ideas/14)
            if (_chaseTarget == null || (_chaseTarget.State.Value != ConditionState.Active && _chaseTarget.State.Value != ConditionState.Hidden))
            {
                SetState(CatState.Return);
                return;
            }

            // 게으름뱅이: 오래 쫓으면 하품하고 포기 (design/cat-ideas/01)
            float giveUp = ChaseGiveUp > 0f && IsGrudged(_chaseTarget.OwnerClientId) ? ChaseGiveUp + _balance.CatGrudgeGiveUpBonus : ChaseGiveUp;
            if (giveUp > 0f && Time.time - _chaseStartedAt >= giveUp)
            {
                Log.Dev($"고양이 [{name}]: 추격 포기 (성격 {Personality.DisplayName}, {giveUp}s)");
                ServerAddGrudge(_chaseTarget.OwnerClientId, _balance.CatGrudgeEscape, "놓침");
                _chaseTarget = null;
                _senses.ConsumeStimulus();
                SetState(CatState.Return);
                return;
            }

            // 코너 감속: 몸이 경로 방향과 어긋날수록 느리게 — 직선은 최고 속도, 지그재그는 실제로 도움된다 (design/cat-design/01-6)
            float align = Mathf.InverseLerp(-0.2f, 0.9f, _movement.SteeringAlignment);
            float chaseSpeed = _balance.CatChaseSpeed * ChaseSpeedMul * Mathf.Lerp(_balance.CatChaseCornerSpeedMul, 1f, align);

            bool seeing = _senses.VisibleTarget == _chaseTarget;
            if (seeing)
            {
                Vector3 pos = _chaseTarget.transform.position;
                // 타깃 속도(위치 차분) — 첫 프레임은 0
                _targetVelocity = _lostSightSince < 0f && _targetPrevPos != Vector3.zero ? (pos - _targetPrevPos) / Mathf.Max(Time.deltaTime, 0.001f) : Vector3.zero;
                _targetPrevPos = pos;
                _lostSightSince = -1f;
                _overshooting = false;
                _lastKnownTargetPos = pos;
                // 예측 추격: 타깃이 가는 방향 1s 앞을 노린다. NavMesh 밖이면 타깃 위치로
                Vector3 lead = pos + Vector3.ClampMagnitude(_targetVelocity, _balance.CatChaseSpeed) * _balance.CatChaseLeadSeconds;
                _movement.MoveTo(CatMovement.Sample(lead, 1.5f, pos), chaseSpeed);
                if (Vector3.Distance(transform.position, pos) <= _balance.CatCaptureRange)
                    SetState(CatState.Capture);
            }
            else
            {
                if (_lostSightSince < 0f)
                {
                    _lostSightSince = Time.time;
                    _targetPrevPos = Vector3.zero;
                    // 오버슛: 마지막 진행 방향으로 3m 더 — 코너를 돌아 "따라 도는" 느낌. NavMesh 밖이면 마지막 목격점
                    Vector3 dir = _targetVelocity; dir.y = 0f;
                    _overshooting = dir.sqrMagnitude > 0.25f;
                    Vector3 goal = _overshooting ? _lastKnownTargetPos + dir.normalized * _balance.CatChaseOvershootMeters : _lastKnownTargetPos;
                    _movement.MoveTo(CatMovement.Sample(goal, 1.5f, _lastKnownTargetPos), chaseSpeed);
                }
                else if (_overshooting && _movement.Arrived)
                {
                    _overshooting = false;
                    _movement.MoveTo(_lastKnownTargetPos, chaseSpeed); // 오버슛 끝 — 마지막 "목격" 지점만 (월핵 금지)
                }
                if (Time.time - _lostSightSince >= _balance.CatLoseSightSeconds)
                {
                    _investigatePos = _lastKnownTargetPos;
                    ServerAddGrudge(_chaseTarget.OwnerClientId, _balance.CatGrudgeEscape, "놓침");
                    _chaseTarget = null;
                    _senses.ConsumeStimulus();
                    // 마지막 목격점 근처에 숨을 곳이 있으면 수색, 없으면 조사 (docs/07)
                    if (TryEnterSearch(_lastKnownTargetPos)) return;
                    SetState(CatState.Suspicious);
                    _movement.MoveTo(_investigatePos, _balance.CatSuspiciousSpeed);
                }
            }
        }

        private void TickCapture()
        {
            // 0.4s 스윙 → 전방 1.2m 판정 (애니 종료 시점 위치 기준 — 회피 가능, docs/07)
            if (_groomUntil > 0f)
            {
                if (Time.time >= _groomUntil) SetState(CatState.Return);
                return;
            }
            if (Time.time < _captureSwingEnd) return;

            foreach (var col in Physics.OverlapSphere(
                transform.position + transform.forward * 0.6f, _balance.CatCaptureRadius,
                LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
            {
                var condition = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetComponent<PlayerCondition>() : null;
                if (condition != null && condition.State.Value == ConditionState.Active)
                {
                    if (IsKitten) { KittenKnockdown(condition); return; } // 아기는 못 잡는다 — 넘어뜨리기만 (design/cat-ideas/07)
                    if (TryStartToy(condition)) return; // 동료가 있으면 바로 끝내지 않고 가지고 논다 (design/cat-ideas/04)
                    condition.ServerSetState(ConditionState.Downed);
                    Log.Dev($"고양이 [{name}] 포획: client {condition.OwnerClientId}");
                    _groomUntil = Time.time + _balance.CatGroomSeconds; // 그루밍(승리 연출)
                    return;
                }
            }
            // 헛스윙 — 추격 재개
            SetState(CatState.Chase);
        }

        private void TickDistracted()
        {
            if (Time.time >= _distractUntil)
            {
                FinishDistractExtras(); // 뇌물 먹기 끝·속았음 깨닫기
                if (_wobbleAfterDistract)
                {
                    _wobbleAfterDistract = false;
                    Log.Dev($"고양이 [{name}]: 캣닢 끝 — 비틀비틀");
                    EnterBlunder(CatBlunderKind.Wobble, _balance.CatWobbleSeconds);
                    return;
                }
                SetState(_stateBeforeDistract == CatState.Distracted ? CatState.Return : _stateBeforeDistract);
                return;
            }
            if (TickBribe()) return; // 뇌물은 그 자리에서 먹는다
            if (_movement.Arrived)
                _movement.MoveTo(_movement.RandomPointAround(_distractPos, 1.5f), _balance.CatDistractedSpeed);
        }

        private void TickReturn()
        {
            if (CheckEscalation()) return;
            if (TryEatBribe()) return;
            if (CheckScent()) return;
            if (CheckCuriosity()) return;
            if (_spots.Length == 0 || _movement.Arrived) SetState(CatState.Patrol);
        }

        // ---- 공통 전이 ----

        private bool CheckEscalation()
        {
            // 직접목격 1s or 2m 내 목격 or 게이지 만땅 → Chase (docs/07)
            if (_senses.VisibleTarget != null &&
                (_senses.ContinuousSightSeconds >= _balance.CatDirectSightChaseSeconds
                 || _senses.CloseSight
                 || _senses.SuspicionGauge.Value >= _balance.CatChaseThreshold))
            {
                _chaseTarget = _senses.VisibleTarget;
                SetState(CatState.Chase);
                return true;
            }
            if ((_senses.ImmediateInvestigate
                 || _senses.SuspicionGauge.Value >= _balance.CatSuspicionThreshold) && _senses.HasNewStimulus)
            {
                EnterSuspicious();
                return true;
            }
            return false;
        }

        private void EnterSuspicious()
        {
            _investigatePos = _senses.LastStimulusPos;
            _senses.ConsumeStimulus();
            SetState(CatState.Suspicious);
        }

        // 복귀는 가장 가까운 스팟으로 — 도착하면 Patrol이 그 스팟 행동부터 이어 간다. 방금 자고 일어난 잠자리는 제외(다시 눕지 않게)
        private Vector3 NearestSpot()
        {
            Vector3 best = transform.position;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _spots.Length; i++)
            {
                // 방금 쓴 "행동 스팟"(잠자리·매복·상자·화장실)으로는 복귀하지 않는다 — 도착하면 그 행동을 또 해서
                // 매복 60s → 복귀 → 같은 매복 스팟 → 또 매복… 무한 반복되던 문제 (고양이 20 소크 테스트)
                bool activitySpot = _spots[i].Type is CatSpotType.Bed or CatSpotType.Ambush or CatSpotType.Box or CatSpotType.Litter;
                if (activitySpot && _recentSpots.Contains(i) && _spots.Length > 1) continue;
                if (_spots[i].Type == CatSpotType.Door) continue; // 문은 복귀 지점이 아니다
                float d = Vector3.Distance(transform.position, _spots[i].Pos);
                if (d < bestDist) { bestDist = d; best = _spots[i].Pos; _spotIndex = i; }
            }
            _dwelling = false;
            return best;
        }
    }
}
