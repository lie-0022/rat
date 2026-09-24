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
        public static bool DevForcedCrouch; // 테스트: 웅크림 고정 (DevRemoteControl)

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

        private float _groundWaitUntil;
        private Vector3 _groundWaitPos;
        private Quaternion _groundWaitRot;
        private Vector3 _lastGroundedPos;
        [SerializeField] private InputActionAsset _inputAsset;

        private Rigidbody _rb;
        private PlayerUpgrades _upgrades;
        private InputAction _moveAction, _jumpAction, _sprintAction, _crouchAction;

        private float _lastGroundedTime = float.NegativeInfinity;
        private float _jumpRequestTime = float.NegativeInfinity;
        private float _staggerUntil = float.NegativeInfinity;
        private bool _isGrounded;
        private bool _isCrouching;
        private bool _crouchToggled;
        private Vector3 _baseScale;

        private const float GroundCastRadius = 0.15f;
        private const float GroundCastDistance = 0.25f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _upgrades = GetComponent<PlayerUpgrades>();
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

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            if (!RatGame.Core.InputFocus.IsUiOpen) _jumpRequestTime = Time.time;
        }

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

        private void Update()
        {
            // 웅크리기: 홀드 기본 (docs/04), 설정에서 눌러서 전환 선택 (docs/12). 그레이박스 단계라 스케일로 콜라이더+비주얼 동시 축소.
            bool uiOpen = InputFocus.IsUiOpen;
            bool wantCrouch;
            if (SettingsService.Current.CrouchToggle)
            {
                if (!uiOpen && _crouchAction.WasPressedThisFrame()) _crouchToggled = !_crouchToggled;
                wantCrouch = _crouchToggled;
            }
            else
            {
                _crouchToggled = false;
                wantCrouch = _crouchAction.IsPressed() && !uiOpen;
            }
            wantCrouch |= DevForcedCrouch;
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
            if (HoldForGround()) return;
            if (transform.position.y < _balance.FallRescueY) { RescueFromFall(); return; }
            CheckGrounded();
            if (_isGrounded) _lastGroundedPos = transform.position + Vector3.up * 0.3f;
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
            // 메뉴 패널 조작 중엔 WASD가 몸을 움직이지 않게
            Vector2 input = IsIncapacitated() || RatGame.Core.InputFocus.IsUiOpen ? Vector2.zero
                : DevAutoWander ? GetWanderInput() : _moveAction.ReadValue<Vector2>();

            // 카메라 기준 입력 → 월드 방향 (docs/04)
            var cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            forward.y = 0f; right.y = 0f;
            Vector3 wishDir = (forward.normalized * input.y + right.normalized * input.x);
            if (DevForcedInput.sqrMagnitude > 0.0001f && !IsIncapacitated())
                wishDir = new Vector3(DevForcedInput.x, 0f, DevForcedInput.y); // 테스트: 월드 방향 직접
            else if (DevAutoWander && !IsIncapacitated() && DevWallsWander.TryDirection(OwnerClientId, transform.position, out var wander))
                wishDir = wander; // 벽 속이면 방에서 방으로 (고양이 123)
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            bool sprinting = !_isCrouching && _sprintAction.IsPressed() && !BlockSprint && !BlockSprintStamina;
            IsSprinting = sprinting && input.sqrMagnitude > 0.01f;
            float speed = _isCrouching ? _balance.CrouchSpeed
                : sprinting ? _balance.SprintSpeed
                : _balance.WalkSpeed;
            speed *= CarrySpeedMultiplier;
            if (_upgrades != null) speed *= _upgrades.MoveSpeedMultiplier; // 상점 업그레이드 (docs/11)
            float accel = _isGrounded ? _balance.GroundAcceleration : _balance.AirAcceleration;

            Vector3 current = _rb.linearVelocity;
            Vector3 targetHorizontal = wishDir * speed;
            Vector3 delta = targetHorizontal - new Vector3(current.x, 0f, current.z);
            Vector3 change = Vector3.ClampMagnitude(delta, accel * Time.fixedDeltaTime);
            _rb.AddForce(change, ForceMode.VelocityChange);

            // 1인칭: 몸은 시선(카메라 yaw)을 따라 돈다 — 옆걸음·뒷걸음이 생김 (2026-09-12 결정)
            var rig = GetComponent<PlayerCameraRig>();
            if (rig != null && rig.enabled)
            {
                _rb.MoveRotation(Quaternion.Euler(0f, rig.Yaw, 0f));
            }
            else if (wishDir.sqrMagnitude > 0.0001f)
            {
                // 카메라 리그 없을 때(자동 배회 봇 등) 예전 규칙: 이동 방향으로 회전
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
            float jump = _balance.JumpImpulse * (_upgrades != null ? _upgrades.JumpPowerMultiplier : 1f); // 상점 업그레이드 (docs/11)
            _rb.AddForce(Vector3.up * jump, ForceMode.VelocityChange);
        }
    }
}
