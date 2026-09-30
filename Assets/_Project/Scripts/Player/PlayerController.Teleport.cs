using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    // 비틀·순간이동·바라보기·바닥 기다리기·떨어짐 구조 — PlayerController.cs가 300줄이라 나눔 (고양이 308)
    public partial class PlayerController
    {
        /// <summary>던진 아이템 피격 등 짧은 조작 불능 (docs/05 — 연출만, 데미지 없음). 호스트가 호출.</summary>
        [Unity.Netcode.ClientRpc]
        public void StaggerClientRpc(float duration)
        {
            if (IsOwner) _staggerUntil = Time.time + duration;
        }

        /// <summary>
        /// 스테이지 도착 시 시작 위치로 이동 (docs/11). 이동은 소유 클라 권한이라 호스트가 옮기면 곧 덮어써진다 — 소유자가 직접 옮긴다.
        /// </summary>
        [Unity.Netcode.ClientRpc]
        public void TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            Place(position, rotation);
            bool ground = HasGroundBelow(position);
            // 생성 스테이지: 이 RPC가 방 스폰보다 먼저 올 수 있다 — 바닥이 생길 때까지 제자리에 붙잡는다 (고양이 59)
            _groundWaitUntil = ground ? 0f : Time.time + _balance.TeleportGroundWaitSeconds;
            _groundWaitPos = position;
            _groundWaitRot = rotation;
            if (!ground) Log.Dev($"텔레포트 → {position:F1} 바닥 없음 — 생길 때까지 기다림"); // 다운 몸·물고 가기는 매 프레임 부르니 평소엔 조용히
        }

        /// <summary>시선 돌리기 — 1인칭 시선이 몸 방향을 정해서 텔레포트 회전만으론 안 돈다. 배치할 때만 (매 프레임 텔레포트엔 안 씀).</summary>
        [Unity.Netcode.ClientRpc]
        public void FaceClientRpc(float yaw, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            var rig = GetComponent<PlayerCameraRig>();
            if (rig != null) rig.SnapYaw(yaw);
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            // 쓰러진 동안은 키네마틱(PlayerDownedBody) — 몸을 따라 0.1s마다 불려서 속도 쓰기가 오류 도배가 됐다 (고양이 126)
            if (!_rb.isKinematic) _rb.linearVelocity = Vector3.zero;
            _rb.position = position;
            _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            // 보간 없이 끊어서 이동 — 원격 사본이 먼 거리를 미끄러져 오지 않게
            GetComponent<Unity.Netcode.Components.NetworkTransform>()?.Teleport(position, rotation, transform.localScale);
        }

        private static bool HasGroundBelow(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, 3f, ~LayerMask.GetMask("Player", "Ragdoll"), QueryTriggerInteraction.Ignore);

        // true면 이번 프레임 이동 처리 안 함 (바닥 기다리는 중)
        private bool HoldForGround()
        {
            if (_groundWaitUntil <= 0f) return false;
            if (HasGroundBelow(_groundWaitPos) || Time.time >= _groundWaitUntil)
            {
                Log.Dev($"텔레포트 바닥 대기 끝 ({(HasGroundBelow(_groundWaitPos) ? "바닥 생김" : "시간 초과")})");
                _groundWaitUntil = 0f;
                Place(_groundWaitPos, _groundWaitRot);
                return false;
            }
            if (!_rb.isKinematic) _rb.linearVelocity = Vector3.zero;
            _rb.position = _groundWaitPos;
            return true;
        }

        // 맵 밖 낙하 안전망 — 가까운 시작 위치(PlayerSpawn)로. 없으면 마지막으로 서 있던 곳
        private void RescueFromFall()
        {
            Vector3 target = _lastGroundedPos; float best = float.MaxValue;
            foreach (var p in GameObject.FindGameObjectsWithTag("PlayerSpawn"))
            {
                Vector3 d = p.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; target = p.transform.position; }
            }
            Log.Dev($"낙하 구조: y {transform.position.y:F0} → {target:F1}");
            Place(target, transform.rotation);
            _groundWaitUntil = HasGroundBelow(target) ? 0f : Time.time + _balance.TeleportGroundWaitSeconds;
            _groundWaitPos = target;
            _groundWaitRot = transform.rotation;
        }
    }
}
