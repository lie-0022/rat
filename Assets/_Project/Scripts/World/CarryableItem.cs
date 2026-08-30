using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 운반 가능한 전리품 (docs/05). 물리는 호스트에서만 시뮬 — 잡기 = 호스트가 GripPoint와
    /// 플레이어 사이에 스프링 조인트 생성. 여러 명이 잡으면 조인트 합산 = 다인 운반 공짜 구현.
    /// maxForce는 mass와 무관하게 고정: 무거우면 질질 끌리는 게 의도(코미디의 원천).
    /// 트레잇(Fragile 등)·파손은 태스크 1-1/1-2에서 확장.
    /// </summary>
    public class CarryableItem : NetworkBehaviour
    {
        [SerializeField] private LootItemSO _data;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private Transform[] _gripPoints;

        public NetworkList<ulong> CarrierIds = new NetworkList<ulong>();
        public NetworkVariable<float> Durability = new NetworkVariable<float>(1f); // Fragile만 사용

        public LootItemSO Data => _data;
        public float Mass => _data != null ? _data.Mass : GetComponent<Rigidbody>().mass;

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

        // 호스트 전용 상태: gripIndex → (잡은 클라, 조인트)
        private readonly Dictionary<int, ulong> _gripOwners = new();
        private readonly Dictionary<ulong, ConfigurableJoint> _joints = new();

        private Rigidbody _rb;
        private float _thrownUntil; // 던져진 직후 1.5s — 이 동안 플레이어 맞으면 비틀거림 (docs/05)

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (_data != null) _rb.mass = _data.Mass; // SO가 물리 질량의 SSOT
            if (Has(ItemTrait.Rolling)) _rb.angularDamping = 0.05f; // 놓으면 굴러간다 (docs/05)
        }

        /// <summary>호스트 전용. 빈 GripPoint 중 플레이어와 가장 가까운 곳에 조인트 생성.</summary>
        public bool ServerTryGrab(ulong clientId, Rigidbody playerBody, Vector3 handLocalOffset)
        {
            if (!IsServer || _joints.ContainsKey(clientId)) return false;

            int grip = FindNearestFreeGrip(playerBody.position);
            if (grip < 0) return false;

            var joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = playerBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = transform.InverseTransformPoint(_gripPoints[grip].position);
            joint.connectedAnchor = handLocalOffset;
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
                // Rolling: 잡기 중 각도 드라이브 끔 — 손에서도 데굴거림 (docs/05)
                positionSpring = Has(ItemTrait.Rolling) ? 0f : _balance.GrabAngularSpring,
                positionDamper = damper * 0.25f,
                maximumForce = _balance.GrabMaxForce
            };
            joint.targetPosition = Vector3.zero;

            if (Has(ItemTrait.Alarming)) // 잡기 소음 (docs/05·06)
                Noise.NoiseSystem.Emit(transform.position, _balance.AlarmingLoudness, Noise.NoiseType.Item, clientId);

            _gripOwners[grip] = clientId;
            _joints[clientId] = joint;
            CarrierIds.Add(clientId);
            Log.Dev($"잡기: client {clientId} → {name} (grip {grip}, 캐리어 {CarrierIds.Count})");
            return true;
        }

        /// <summary>호스트 전용. thrown=true면 던지기 임펄스 적용.</summary>
        public void ServerRelease(ulong clientId, bool thrown = false, Vector3 throwDir = default, float charge = 0.5f)
        {
            if (!IsServer || !_joints.TryGetValue(clientId, out var joint)) return;

            Destroy(joint);
            _joints.Remove(clientId);
            foreach (var kv in _gripOwners)
            {
                if (kv.Value == clientId) { _gripOwners.Remove(kv.Key); break; }
            }
            CarrierIds.Remove(clientId);

            if (thrown)
            {
                _rb.AddForce(throwDir.normalized * _balance.GetThrowImpulse(charge, _rb.mass), ForceMode.Impulse);
                _thrownUntil = Time.time + 1.5f;
            }
            Log.Dev($"놓기: client {clientId} ← {name}{(thrown ? " (던짐)" : "")}");
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
            if (!IsServer || _joints.Count == 0) return;

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
    }
}
