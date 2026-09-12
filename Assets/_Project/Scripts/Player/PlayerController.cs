using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 이동·점프·웅크리기 (docs/04). 소유 클라에서만 시뮬 — 원격 사본은 kinematic + CNT 보간 (docs/03).
    /// velocity 직접 대입 금지: AddForce로 목표 속도 추종 (운반 물리와 충돌 방지).
    /// 스태미나·상태이상·소음은 W2 태스크에서 별도 컴포넌트로.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        /// <summary>무인 테스트용 자동 배회 (DevAutoConnect -autowander). 사람 입력 대신 펄린 노이즈 방향.</summary>
        public static bool DevAutoWander;
        /// <summary>무인 테스트용 고정 입력 (월드 XZ). 0이 아니면 사람 입력 대신 이 방향으로 걷는다.</summary>
        public static Vector2 DevForcedInput;

        /// <summary>프리팹 기준 스케일 (쥐 비율). 웅크림 판정(스케일 절반) 공유 기준 — CatSenses·이미터가 사용.</summary>
        public const float BaseScaleY = 0.6f;

        // 운반 페널티 (PlayerCarryController가 소유 클라에서 세팅 — docs/05)
        public float CarrySpeedMultiplier { get; set; } = 1f;
        public bool BlockSprint { get; set; }
        public bool BlockJump { get; set; }
        // 스태미나 고갈 sprint 차단 (PlayerStamina — docs/04)
        public bool BlockSprintStamina { get; set; }
        /// <summary>지금 실제로 달리는 중인가 (스태미나 소모 판정용).</summary>
        public bool IsSprinting { get; private set; }

        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private Rigidbody _rb;
        private InputAction _moveAction, _jumpAction, _sprintAction, _crouchAction;

        private float _lastGroundedTime = float.NegativeInfinity;
        private float _jumpRequestTime = float.NegativeInfinity;
        private float _staggerUntil = float.NegativeInfinity;
        private bool _isGrounded;
        private bool _isCrouching;
        private Vector3 _baseScale;

        private const float GroundCastRadius = 0.15f;
        private const float GroundCastDistance = 0.25f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _baseScale = transform.localScale;
        }

        public override void OnNetworkSpawn()
        {
            // 원격 사본은 물리 끄고 보간만 (docs/03 — 플레이어는 소유 클라 권한)
            _rb.isKinematic = !IsOwner;
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            var map = _inputAsset.FindActionMap("Player", true);
            _moveAction = map.FindAction("Move", true);
            _jumpAction = map.FindAction("Jump", true);
            _sprintAction = map.FindAction("Sprint", true);
            _crouchAction = map.FindAction("Crouch", true);
            map.Enable();
            _jumpAction.performed += OnJumpPerformed;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            _jumpAction.performed -= OnJumpPerformed;
            _inputAsset.FindActionMap("Player").Disable();
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx) => _jumpRequestTime = Time.time;

        /// <summary>던진 아이템 피격 등 짧은 조작 불능 (docs/05 — 연출만, 데미지 없음). 호스트가 호출.</summary>
        [Unity.Netcode.ClientRpc]
        public void StaggerClientRpc(float duration)
        {
            if (IsOwner) _staggerUntil = Time.time + duration;
        }

        private void Update()
        {
            // 웅크리기: 홀드 기본 (docs/04). 그레이박스 단계라 스케일로 콜라이더+비주얼 동시 축소.
            bool wantCrouch = _crouchAction.IsPressed();
            if (wantCrouch != _isCrouching)
            {
                _isCrouching = wantCrouch;
                var s = _baseScale;
                if (_isCrouching) s.y *= 0.5f;
                transform.localScale = s;
            }
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            ApplyMovement();
            TryJump();
        }

        private void CheckGrounded()
        {
            // 자기 자신(Player 레이어)은 제외 — 쥐가 쥐를 밟는 건 이후 웃기면 사양으로 재검토
            // 캐스트는 캡슐 "중심"에서 발밑까지: 반높이(스케일 반영) − 반경 + 여유 0.25 (docs/04)
            int mask = ~LayerMask.GetMask("Player", "Ragdoll");
            float halfHeight = 1f * transform.localScale.y; // 프리미티브 캡슐 height 2 기준
            float castDistance = halfHeight - GroundCastRadius + GroundCastDistance;
            _isGrounded = Physics.SphereCast(transform.position, GroundCastRadius, Vector3.down,
                out _, castDistance, mask, QueryTriggerInteraction.Ignore);
            if (_isGrounded) _lastGroundedTime = Time.time;
        }

        private bool IsIncapacitated()
        {
            if (Time.time < _staggerUntil) return true;
            var condition = GetComponent<PlayerCondition>();
            return condition != null && condition.State.Value != ConditionState.Active;
        }

        private void ApplyMovement()
        {
            Vector2 input = IsIncapacitated() ? Vector2.zero
                : DevAutoWander ? GetWanderInput() : _moveAction.ReadValue<Vector2>();

            // 카메라 기준 입력 → 월드 방향 (docs/04)
            var cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            forward.y = 0f; right.y = 0f;
            Vector3 wishDir = (forward.normalized * input.y + right.normalized * input.x);
            if (DevForcedInput.sqrMagnitude > 0.0001f && !IsIncapacitated())
                wishDir = new Vector3(DevForcedInput.x, 0f, DevForcedInput.y); // 테스트: 월드 방향 직접
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            bool sprinting = !_isCrouching && _sprintAction.IsPressed() && !BlockSprint && !BlockSprintStamina;
            IsSprinting = sprinting && input.sqrMagnitude > 0.01f;
            float speed = _isCrouching ? _balance.CrouchSpeed
                : sprinting ? _balance.SprintSpeed
                : _balance.WalkSpeed;
            speed *= CarrySpeedMultiplier;
            float accel = _isGrounded ? _balance.GroundAcceleration : _balance.AirAcceleration;

            Vector3 current = _rb.linearVelocity;
            Vector3 targetHorizontal = wishDir * speed;
            Vector3 delta = targetHorizontal - new Vector3(current.x, 0f, current.z);
            Vector3 change = Vector3.ClampMagnitude(delta, accel * Time.fixedDeltaTime);
            _rb.AddForce(change, ForceMode.VelocityChange);

            // 이동 방향으로 회전 (카메라 방향 아님 — docs/04)
            if (wishDir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(wishDir);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot,
                    _balance.RotationSlerp * Time.fixedDeltaTime));
            }
        }

        private Vector2 GetWanderInput()
        {
            float seed = OwnerClientId * 7.31f;
            float angle = Mathf.PerlinNoise(Time.time * 0.15f, seed) * Mathf.PI * 4f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.8f;
        }

        private void TryJump()
        {
            if (BlockJump || IsIncapacitated()) return;
            bool buffered = Time.time - _jumpRequestTime <= _balance.JumpBuffer;
            bool coyote = Time.time - _lastGroundedTime <= _balance.CoyoteTime;
            if (!buffered || !coyote) return;

            _jumpRequestTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            var v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v; // 점프 순간 수직 속도 리셋만 — 수평은 유지
            _rb.AddForce(Vector3.up * _balance.JumpImpulse, ForceMode.VelocityChange);
        }
    }
}
