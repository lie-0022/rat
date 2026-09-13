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
    public class CarryableItem : NetworkBehaviour
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
        public float Mass => _data != null ? _data.Mass : GetComponent<Rigidbody>().mass;

        /// <summary>동시에 잡을 수 있는 인원 (대형 = 자동 슬롯 수). 클라에서도 계산 가능 — HUD용.</summary>
        public int CarrySlotCount => IsHeavy ? HeavySlotCount : (_gripPoints != null ? _gripPoints.Length : 0);
        public bool IsCarrySlotsFull => CarrierIds.Count >= CarrySlotCount;

        /// <summary>정산 가치: 금 갔으면(Durability<0.5) 50% (docs/05).</summary>
        public int EffectiveValue
        {
            get
            {
                if (_data == null) return 0;
                if (!_data.Has(ItemTrait.Fragile)) return _data.BaseValue;
                return Durability.Value < 0.5f
                    ? Mathf.RoundToInt(_data.BaseValue * _balance.CrackedValueMultiplier)
                    : _data.BaseValue;
            }
        }

        private bool Has(ItemTrait t) => _data != null && _data.Has(t);

        // 호스트 전용 상태: grip(소형) 또는 slot(대형) 인덱스 → 잡은 클라 / 클라 → 조인트
        private readonly Dictionary<int, ulong> _gripOwners = new();
        private readonly Dictionary<ulong, ConfigurableJoint> _joints = new();

        // 자동 대형: 긴 변 양쪽 면 중앙. LocalAnchor = 쥐가 서는 점(면에서 바깥으로 stand 거리) — 조인트가 이 점을 쥐 몸에 붙인다
        private const int HeavySlotCount = 2;
        private struct CarrySlot { public Vector3 LocalAnchor; }
        private CarrySlot[] _carrySlots;

        private Rigidbody _rb;
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
            if (_data != null) _rb.mass = _data.Mass; // SO가 물리 질량의 SSOT
            if (Has(ItemTrait.Rolling)) _rb.angularDamping = 0.05f; // 놓으면 굴러간다 (docs/05)
        }

        public override void OnNetworkDespawn() => Pocketed.OnValueChanged -= OnPocketedChanged;

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

        /// <summary>호스트 전용. 소형: 빈 GripPoint 중 플레이어와 가장 가까운 곳에 조인트 생성.</summary>
        public bool ServerTryGrab(ulong clientId, Rigidbody playerBody, Vector3 handLocalOffset)
        {
            if (!IsServer || _joints.ContainsKey(clientId)) return false;

            int grip = FindNearestFreeGrip(playerBody.position);
            if (grip < 0) return false;

            // Rolling: 잡기 중 각도 드라이브 끔 — 손에서도 데굴거림 (docs/05)
            float angularSpring = Has(ItemTrait.Rolling) ? 0f : _balance.GrabAngularSpring;
            var joint = CreateJoint(transform.InverseTransformPoint(_gripPoints[grip].position),
                                    playerBody, handLocalOffset, angularSpring);
            RegisterCarrier(clientId, grip, joint);
            return true;
        }

        /// <summary>
        /// 호스트 전용. 대형: 자동 대형 자리 배정 + 조인트. 비어 있으면 가장 가까운 자리,
        /// 누가 잡고 있으면 점유 자리에서 가장 먼(반대편) 자리. standWorld = 쥐가 가서 서야 할 위치.
        /// </summary>
        public bool ServerTryGrabSlot(ulong clientId, Rigidbody playerBody, Vector3 ratAnchorLocal, out Vector3 standWorld)
        {
            standWorld = default;
            if (!IsServer || !IsHeavy || _joints.ContainsKey(clientId)) return false;

            int slot = ChooseCarrySlot(playerBody.position);
            if (slot < 0) return false;

            standWorld = transform.TransformPoint(_carrySlots[slot].LocalAnchor);
            // 각도 드라이브 0: 쥐 몸 회전(시선)이 물건 회전으로 전달되지 않게. 방향은 두 자리의 위치가 잡는다
            var joint = CreateJoint(_carrySlots[slot].LocalAnchor, playerBody, ratAnchorLocal, 0f);
            RegisterCarrier(clientId, slot, joint);
            return true;
        }

        private ConfigurableJoint CreateJoint(Vector3 anchorLocal, Rigidbody playerBody, Vector3 connectedAnchor, float angularSpring)
        {
            var joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = playerBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = anchorLocal;
            joint.connectedAnchor = connectedAnchor;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;

            // Wobbly: damper 1/4 — 출렁임 증폭 (docs/05 트레잇)
            float damper = Has(ItemTrait.Wobbly) ? _balance.GrabDamper * 0.25f : _balance.GrabDamper;
            var drive = new JointDrive
            {
                positionSpring = _balance.GrabSpring,
                positionDamper = damper,
                maximumForce = _balance.GrabMaxForce
            };
            joint.xDrive = joint.yDrive = joint.zDrive = drive;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = angularSpring,
                positionDamper = damper * 0.25f,
                maximumForce = _balance.GrabMaxForce
            };
            joint.targetPosition = Vector3.zero;
            return joint;
        }

        private void RegisterCarrier(ulong clientId, int index, ConfigurableJoint joint)
        {
            if (Has(ItemTrait.Alarming)) // 잡기 소음 (docs/05·06)
                Noise.NoiseSystem.Emit(transform.position, _balance.AlarmingLoudness, Noise.NoiseType.Item, clientId);

            _gripOwners[index] = clientId;
            _joints[clientId] = joint;
            CarrierIds.Add(clientId);
            Log.Dev($"잡기: client {clientId} → {name} ({(IsHeavy ? "자리" : "grip")} {index}, 캐리어 {CarrierIds.Count}/{CarrySlotCount})");
        }

        /// <summary>호스트 전용. thrown=true면 던지기 임펄스 적용.</summary>
        public void ServerRelease(ulong clientId, bool thrown = false, Vector3 throwDir = default, float charge = 0.5f,
                                  float throwPower = 1f)
        {
            if (!IsServer || !_joints.TryGetValue(clientId, out var joint)) return;

            Destroy(joint);
            _joints.Remove(clientId);
            foreach (var kv in _gripOwners)
            {
                if (kv.Value == clientId) { _gripOwners.Remove(kv.Key); break; }
            }
            CarrierIds.Remove(clientId);
            LastCarrierId = clientId; // 내려놓고 납품될 때 기여자 집계용

            if (thrown)
            {
                _rb.AddForce(throwDir.normalized * _balance.GetThrowImpulse(charge, _rb.mass) * throwPower, ForceMode.Impulse);
                _thrownUntil = Time.time + 1.5f;
            }
            Log.Dev($"놓기: client {clientId} ← {name}{(thrown ? " (던짐)" : "")}");
        }

        /// <summary>호스트 전용. 조인트 해제 후 지정 위치에 정지 상태로 놓기 (토글 내려놓기).</summary>
        public void ServerPutDown(ulong clientId, Vector3 position)
        {
            ServerRelease(clientId);
            if (_joints.Count > 0) return; // 다른 캐리어가 아직 들고 있으면 위치 강제 안 함
            _rb.position = position;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        /// <summary>호스트 전용. 다운·정산 시 전원 강제 해제 (docs/05).</summary>
        public void ServerReleaseAll()
        {
            if (!IsServer) return;
            var carriers = new List<ulong>();
            foreach (var id in CarrierIds) carriers.Add(id);
            foreach (var id in carriers) ServerRelease(id);
        }

        private float _nextSlipperyRoll;

        private void FixedUpdate()
        {
            if (!IsServer) return;
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
            Noise.NoiseSystem.Emit(transform.position, _balance.BreakLoudness, Noise.NoiseType.Break);
            EventBus.RaiseLootBroken(_data);
            NetworkObject.Despawn();
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
                Noise.NoiseSystem.Emit(transform.position, loudness, Noise.NoiseType.Impact);
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

        // ---- 자동 대형 ----

        // 콜라이더(로컬) 기준: 긴 수평축을 따라 잡도록 짧은 축 방향 양쪽 면의 윗면 중앙에 자리.
        // stand 거리는 월드 미터라 로컬로 환산(스케일 나눔). 아이템 모양이 바뀌어도 자동으로 따라감
        private void EnsureCarrySlots()
        {
            if (_carrySlots != null) return;
            var box = GetComponent<BoxCollider>();
            Vector3 center = box != null ? box.center : Vector3.zero;
            Vector3 half = box != null ? box.size * 0.5f : Vector3.one * 0.5f;
            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f), sz = Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
            bool longIsZ = half.z * sz >= half.x * sx;
            float stand = _balance.CarrySlotStandDistance;

            _carrySlots = new CarrySlot[HeavySlotCount];
            for (int i = 0; i < HeavySlotCount; i++)
            {
                float sign = i == 0 ? 1f : -1f;
                Vector3 anchor = longIsZ
                    ? new Vector3(center.x + sign * (half.x + stand / sx), center.y + half.y, center.z)
                    : new Vector3(center.x, center.y + half.y, center.z + sign * (half.z + stand / sz));
                _carrySlots[i] = new CarrySlot { LocalAnchor = anchor };
            }
        }

        private int ChooseCarrySlot(Vector3 ratPos)
        {
            EnsureCarrySlots();
            int best = -1;
            float bestScore = float.MaxValue;
            bool anyTaken = _gripOwners.Count > 0;
            for (int i = 0; i < _carrySlots.Length; i++)
            {
                if (_gripOwners.ContainsKey(i)) continue;
                Vector3 stand = transform.TransformPoint(_carrySlots[i].LocalAnchor);
                float score;
                if (anyTaken)
                {
                    float nearestTaken = float.MaxValue;
                    foreach (int taken in _gripOwners.Keys)
                        nearestTaken = Mathf.Min(nearestTaken,
                            Vector3.Distance(stand, transform.TransformPoint(_carrySlots[taken].LocalAnchor)));
                    score = -nearestTaken; // 점유 자리에서 멀수록 좋음 (반대편)
                }
                else
                {
                    score = Vector3.Distance(stand, ratPos); // 첫 사람은 가까운 자리
                }
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
