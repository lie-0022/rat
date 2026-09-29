using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>운반 물건 — 호스트의 잡기(소형 손잡이·대형 자리)·놓기·던지기·내려놓기. CarryableItem.cs에서 옮김 (고양이 205).</summary>
    public partial class CarryableItem
    {
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
            _lastReleaseTime = Time.time;

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
    }
}
