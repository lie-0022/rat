using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Noise
{
    /// <summary>
    /// 플레이어 소음원 (docs/06). 발걸음·착지는 클라를 신뢰하지 않고 **호스트가 위치 델타로 직접 계산**
    /// (ClientNetworkTransform 위치는 호스트도 안다 — RPC 절약). 웅크림 판정은 동기화된 스케일로
    /// (그레이박스 단계 규약: 웅크리면 y 스케일 50%). Squeak만 소유 클라 입력 → ServerRpc.
    /// 바닥 재질 배수(hardfloor/rug)는 재질 에셋 생기는 아트 단계에서 — 지금은 ×1.
    /// </summary>
    public class PlayerNoiseEmitter : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction _squeakAction;

        // 호스트 전용 추정 상태
        private Vector3 _lastPos;
        private float _nextFootstepTime;
        private bool _wasAirborne;
        private float _peakY;
        private float _baseScaleY;

        public override void OnNetworkSpawn()
        {
            NoiseSystem.Configure(_balance);
            _lastPos = transform.position;
            _baseScaleY = transform.localScale.y;

            if (!IsOwner) return;
            _squeakAction = _inputAsset.FindActionMap("Player", true).FindAction("Squeak", true);
            _squeakAction.performed += OnSqueak;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && _squeakAction != null) _squeakAction.performed -= OnSqueak;
        }

        private void OnSqueak(InputAction.CallbackContext ctx) => SqueakServerRpc();

        [ServerRpc]
        private void SqueakServerRpc()
        {
            NoiseSystem.Emit(transform.position, _balance.SqueakLoudness, NoiseType.Squeak, OwnerClientId);
            SqueakClientRpc(); // 전 클라 연출 (애니·사운드는 아트 단계 — 지금은 로그 지점)
        }

        [ClientRpc]
        private void SqueakClientRpc() => Log.Dev($"찍찍! client {OwnerClientId}");

        private void FixedUpdate()
        {
            if (!IsServer) return;

            Vector3 pos = transform.position;
            Vector3 delta = pos - _lastPos;
            _lastPos = pos;
            float horizontalSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / Time.fixedDeltaTime;

            // 캡슐 중심 → 발밑 + 여유 (스케일=웅크림 반영)
            float castDistance = 1f * transform.localScale.y + 0.35f;
            bool grounded = Physics.Raycast(pos, Vector3.down, castDistance,
                ~LayerMask.GetMask("Player", "Ragdoll"), QueryTriggerInteraction.Ignore);
            bool crouching = transform.localScale.y < _baseScaleY * 0.75f;

            // 착지 소음 (낙하 1m+)
            if (!grounded)
            {
                if (!_wasAirborne) _peakY = pos.y;
                else _peakY = Mathf.Max(_peakY, pos.y);
                _wasAirborne = true;
            }
            else if (_wasAirborne)
            {
                _wasAirborne = false;
                // 재접지는 발밑 0.35m 여유에서 미리 감지되므로 그만큼 낙하 높이에 보정
                float fallHeight = _peakY - pos.y + 0.35f;
                float landing = _balance.GetLandingLoudness(fallHeight);
                if (landing > 0f)
                    NoiseSystem.Emit(pos, landing, NoiseType.Impact, OwnerClientId);
                GetComponent<Player.PlayerCondition>()?.ServerOnLanded(fallHeight); // 6m+ 기절 (docs/04)
            }

            // 발걸음 (웅크림 0 — docs/06)
            if (!grounded || crouching || horizontalSpeed < 1f) return;
            if (Time.time < _nextFootstepTime) return;

            bool running = horizontalSpeed > (_balance.WalkSpeed + _balance.SprintSpeed) * 0.5f;
            float loudness = running ? _balance.FootstepRunLoudness : _balance.FootstepWalkLoudness;
            var pipe = running ? World.PipeEcho.Above(pos) : null;
            _nextFootstepTime = Time.time + (running ? _balance.FootstepRunInterval : _balance.FootstepWalkInterval);
            NoiseSystem.Emit(pos, loudness, NoiseType.Footstep, OwnerClientId);
            if (pipe != null)
            {
                // 쇠관 울림 (고양이 87) — 관 속 소리는 벽에 막히니 양쪽 입구 밖에서 크게
                pipe.Mouths(out var a, out var b);
                float echo = loudness * _balance.PipeEchoMultiplier;
                NoiseSystem.Emit(a, echo, NoiseType.Footstep, OwnerClientId);
                NoiseSystem.Emit(b, echo, NoiseType.Footstep, OwnerClientId);
            }
        }

#if UNITY_EDITOR
        // 소음 반경 기즈모 (docs/06 수용 기준 — 에디터 전용, 최근 1.2초)
        private void OnDrawGizmos()
        {
            foreach (var e in NoiseSystem.Recent)
            {
                if (e.Loudness <= 0f || Time.time - e.Time > 1.2f) continue;
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                Gizmos.DrawWireSphere(e.Pos, NoiseSystem.GetRadius(e));
            }
        }
#endif
    }
}
