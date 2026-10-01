using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 운반 가능한 전리품 (docs/05). 물리는 호스트에서만 시뮬 — 잡기 = 호스트가 아이템과
    /// 플레이어 사이에 스프링 조인트 생성. 여러 명이 잡으면 조인트 합산 = 다인 운반 공짜 구현.
    /// maxForce는 mass와 무관하게 고정: 무거우면 질질 끌리는 게 의도(코미디의 원천).
    ///  - 소형: GripPoint에 손 앵커로 매단다.
    ///  - 대형: "자동 대형" — 콜라이더 긴 변 양쪽에 잡는 자리(CarrySlot) 2개를 런타임 계산.
    ///    조인트의 쥐 쪽 앵커를 몸 수직축에 두고 각도 드라이브를 끄므로, 쥐가 시선 따라 몸을 돌려도 물건이 안 휘둘린다.
    /// </summary>
    public partial class CarryableItem : NetworkBehaviour
    {
        [SerializeField] private LootItemSO _data;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private Transform[] _gripPoints; // 소형 전용. 대형은 자동 슬롯을 쓰므로 무시

        public NetworkList<ulong> CarrierIds = new NetworkList<ulong>();
        public NetworkVariable<float> Durability = new NetworkVariable<float>(1f); // Fragile만 사용
        /// <summary>인벤 주머니에 들어간 상태 — 안 보이고, 충돌 없고, 주인을 따라다님 (호스트 기록).</summary>
        public NetworkVariable<bool> Pocketed = new NetworkVariable<bool>(false);
        private Transform _pocketOwner;

        public LootItemSO Data => _data;
        /// <summary>대형/특수 티어 = 손에 못 넣음, 자동 대형 자리에서 끌거나 여럿이 든다.</summary>
        public bool IsHeavy => _data != null && (_data.Tier == LootTier.Large || _data.Tier == LootTier.Special);
        /// <summary>마지막으로 놓은 클라 (호스트 전용 값) — 내려놓은 뒤 납품 판정 시 기여자.</summary>
        public ulong LastCarrierId { get; private set; }
        private float _lastReleaseTime = -99f;
        /// <summary>마지막으로 쥐 손을 떠난 시각 (뇌물 판정 — 방금 내려놓은 치즈).</summary>
        public float LastReleaseTime => _lastReleaseTime;
        /// <summary>소음 귀속 (2026-09-24): 들고 있으면 첫 캐리어, 놓은·던진 지 3s 안이면 마지막 캐리어, 그 밖은 환경(null).</summary>
        public ulong? AttributedClient =>
            CarrierIds.Count > 0 ? CarrierIds[0]
            : Time.time - _lastReleaseTime <= _balance.NoiseAttributionSeconds ? LastCarrierId : (ulong?)null;
        public float Mass => _data != null ? _data.Mass : GetComponent<Rigidbody>().mass;

        /// <summary>동시에 잡을 수 있는 인원 (대형 = 자동 슬롯 수). 클라에서도 계산 가능 — HUD용.</summary>
        public int CarrySlotCount => IsHeavy ? HeavySlotCount : (_gripPoints != null ? _gripPoints.Length : 0);
        private int HeavySlotCount => _balance != null ? _balance.HeavyCarrySlots(Mass) : 2; // 특대 4 (고양이 212)
        public bool IsCarrySlotsFull => CarrierIds.Count >= CarrySlotCount;

        /// <summary>금 간 Fragile (Durability&lt;0.5) — 정산 가치 감소, HUD "금 감" 표시가 같은 기준을 쓴다.</summary>
        public bool IsCracked => _data != null && _data.Has(ItemTrait.Fragile) && Durability.Value < 0.5f;

        /// <summary>정산 가치: 금 갔으면(Durability<0.5) 50% (docs/05).</summary>
        public int EffectiveValue
        {
            get
            {
                if (_data == null) return 0;
                return IsCracked
                    ? Mathf.RoundToInt(_data.BaseValue * _balance.CrackedValueMultiplier)
                    : _data.BaseValue;
            }
        }

        private bool Has(ItemTrait t) => _data != null && _data.Has(t);

        // 호스트 전용 상태: grip(소형) 또는 slot(대형) 인덱스 → 잡은 클라 / 클라 → 조인트
        private readonly Dictionary<int, ulong> _gripOwners = new();
        private readonly Dictionary<ulong, ConfigurableJoint> _joints = new();

        // 자동 대형: 긴 변 양쪽 면 중앙. LocalAnchor = 쥐가 서는 점(면에서 바깥으로 stand 거리) — 조인트가 이 점을 쥐 몸에 붙인다
        private struct CarrySlot { public Vector3 LocalAnchor; }
        private CarrySlot[] _carrySlots;

        private Rigidbody _rb;
        private Vector3 _spawnPos;  // 맵 밖으로 떨어지면 돌아올 자리 (고양이 343)
        private float _thrownUntil; // 던져진 직후 1.5s — 이 동안 플레이어 맞으면 비틀거림 (docs/05)

        /// <summary>던져진 직후인가 (CatLure 털실뭉치 착지 판정용 — docs/07).</summary>
        public bool IsRecentlyThrown => Time.time <= _thrownUntil;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            Pocketed.OnValueChanged += OnPocketedChanged;
            ApplyPocketVisual(Pocketed.Value);
            if (!IsServer) return;
            _spawnPos = transform.position;
            if (_data != null) _rb.mass = _data.Mass; // SO가 물리 질량의 SSOT
            if (Has(ItemTrait.Rolling)) _rb.angularDamping = 0.05f; // 놓으면 굴러간다 (docs/05)
        }

        public override void OnNetworkDespawn()
        {
            Pocketed.OnValueChanged -= OnPocketedChanged;
            // 씬에 놓인 물건은 적립·파괴·먹기 때 파괴 대신 디스폰만 된다(DespawnSafe) — 모두의 화면에서 끈다 (고양이 134)
            if (!NetworkObject.InScenePlaced) return;
            gameObject.SetActive(false);
            if (Run.RunManager.Instance != null && Run.RunManager.Instance.AcceptsDeposits) Log.Dev($"씬 물건 끔: {name}"); // 2인 검증용 — 씬 언로드 때 일괄 디스폰은 안 찍게
        }

        private void OnPocketedChanged(bool _, bool now) => ApplyPocketVisual(now);

        // 전 클라 공통: 주머니 안이면 렌더러·콜라이더 끔 (납품 트리거에도 안 걸림)
        private void ApplyPocketVisual(bool pocketed)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !pocketed;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = !pocketed;
        }

        /// <summary>호스트 전용. 조인트 해제 후 주머니에 넣기 — 물리 정지, 주인 따라다님.</summary>
        public void ServerPocket(ulong clientId, Transform owner)
        {
            if (!IsServer) return;
            ServerRelease(clientId);
            _pocketOwner = owner;
            _rb.isKinematic = true;
            Pocketed.Value = true;
        }

        /// <summary>호스트 전용. 주머니에서 꺼내 지정 위치에 놓기 (조인트는 호출부가 다시 건다).</summary>
        public void ServerUnpocket(Vector3 worldPos)
        {
            if (!IsServer) return;
            Pocketed.Value = false;
            _pocketOwner = null;
            _rb.isKinematic = false;
            _rb.position = worldPos;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // 잡기·놓기·던지기 → CarryableItem.Grab.cs, 대형 잡는 자리 → CarryableItem.Slots.cs (고양이 205)

        private float _nextSlipperyRoll;

        private void FixedUpdate()
        {
            if (!IsServer) return;
            // 맵 밖으로 떨어진 물건은 처음 놓인 자리로 — 쥐(PlayerController)와 같은 기준. 뚫림은 막았지만(고양이 342) 다른 길로 빠져도 영영 잃지 않게 (고양이 343)
            if (transform.position.y < _balance.FallRescueY && _joints.Count == 0 && !Pocketed.Value)
            {
                _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero;
                _rb.position = _spawnPos + Vector3.up * 0.5f;
                transform.position = _rb.position;
                Log.Dev($"물건 떨어짐 구조: {name} → {_spawnPos:F1}");
            }
            // 던진 뒤 1.5초 지나면 다시 Discrete — 연속 충돌은 비싸고, 들고 다닐 땐 조인트라 필요 없다 (고양이 342)
            if (_rb.collisionDetectionMode != CollisionDetectionMode.Discrete && Time.time > _thrownUntil) _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            if (Pocketed.Value && _pocketOwner != null)
            {
                _rb.MovePosition(_pocketOwner.position); // 주머니 안: 주인과 함께 이동 (동기화 범위 유지)
                return;
            }
            if (_joints.Count == 0) return;

            // 거리 초과 시 끊김 (docs/05 판정 플로우 5)
            List<ulong> broken = null;
            foreach (var kv in _joints)
            {
                var body = kv.Value.connectedBody;
                if (body == null || Vector3.Distance(transform.position, body.position) > _balance.GrabBreakDistance)
                    (broken ??= new List<ulong>()).Add(kv.Key);
            }
            if (broken != null)
                foreach (var id in broken) ServerRelease(id);

            // Slippery: 잡기 유지 중 3s마다 12% 강제 놓침 (docs/05)
            if (Has(ItemTrait.Slippery) && _joints.Count > 0 && Time.time >= _nextSlipperyRoll)
            {
                _nextSlipperyRoll = Time.time + _balance.SlipperyInterval;
                if (UnityEngine.Random.value < _balance.SlipperyChance)
                {
                    Log.Dev($"미끌! {name}");
                    ServerReleaseAll();
                }
            }
        }

        /// <summary>Fragile 파괴 (docs/05): 소음 60 + 가치 0 + LootBroken + 디스폰. 파편 파티클은 아트 단계.</summary>
        private void ServerBreak()
        {
            Log.Dev($"파괴: {name}");
            ServerReleaseAll();
            Noise.NoiseSystem.Emit(transform.position, _balance.BreakLoudness, Noise.NoiseType.Break, AttributedClient);
            EventBus.RaiseLootBroken(_data);
            NetworkObject.DespawnSafe();
        }

        private float _nextImpactNoiseTime; // 연쇄 충돌 스팸 방지

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;
            float impact = collision.relativeVelocity.magnitude;

            // 충돌 소음 (docs/06: impact × mass계수, 최대 70) — 호스트 판정
            if (impact > 1.5f && Time.time >= _nextImpactNoiseTime)
            {
                _nextImpactNoiseTime = Time.time + 0.2f;
                float loudness = _balance.GetImpactLoudness(impact, _rb.mass);
                if (Has(ItemTrait.Alarming)) loudness = Mathf.Max(loudness, _balance.AlarmingLoudness);
                Noise.NoiseSystem.Emit(transform.position, loudness, Noise.NoiseType.Impact, AttributedClient);
            }

            // Fragile 파손 (docs/05): 문턱 초과 충돌 → 내구도 감소 → 0이면 파괴
            if (Has(ItemTrait.Fragile))
            {
                float damage = _balance.GetFragileDamage(impact);
                if (damage > 0f)
                {
                    Durability.Value = Mathf.Max(0f, Durability.Value - damage);
                    Log.Dev($"파손 진행: {name} 내구도 {Durability.Value:F2} (충돌 {impact:F1}m/s)");
                    if (Durability.Value <= 0f) { ServerBreak(); return; }
                }
            }

            // 던진 아이템에 맞은 플레이어: 비틀거림 연출만, 데미지 없음 — 트롤 허용 지점 (docs/05)
            if (Time.time > _thrownUntil || impact < 1.5f) return;
            var pc = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<Player.PlayerController>() : null;
            if (pc == null) return;
            pc.StaggerClientRpc(_balance.ThrowHitStagger);
            Log.Dev($"명중: {name} → client {pc.OwnerClientId} 비틀거림");
        }

        private int FindNearestFreeGrip(Vector3 fromPos)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _gripPoints.Length; i++)
            {
                if (_gripOwners.ContainsKey(i)) continue;
                float d = Vector3.Distance(_gripPoints[i].position, fromPos);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }
    }
}
