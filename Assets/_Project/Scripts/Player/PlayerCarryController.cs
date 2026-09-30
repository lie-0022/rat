using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 한 손 운반 + 인벤 슬롯 (2026-09-12 팀 결정: 1인칭, 인벤 2칸, 선택 슬롯 = 손에 든 것).
    ///  - 소형: 좌클릭으로 집으면 선택 슬롯(없으면 빈 슬롯)에 들어가고, 선택된 슬롯 물건만 손 앵커에 조인트.
    ///    다른 슬롯 물건은 "주머니"(CarryableItem.Pocketed — 안 보이고 주인을 따라다님). 1·2키/휠로 전환.
    ///  - 대형(IsHeavy): 슬롯 안 씀. 손 앞 앵커에 매달아 끌기/여럿이 들기. 전환 불가, 던지기 불가.
    ///  - 우클릭 홀드 던지기(차지), 차지 중 좌클릭 취소.
    /// 호스트 권위: 슬롯·손 아이템은 서버 기록, 선택 슬롯만 소유 클라가 쓴다.
    /// </summary>
    public partial class PlayerCarryController : NetworkBehaviour
    {
        /// <summary>인벤 칸 수 — 기본은 Balance.BaseCarrySlots, 상점 업그레이드로 늘어난다 (PlayerUpgrades가 서버에서 맞춤).</summary>
        public int SlotCount => Slots.Count;

        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private Transform _handAnchor;

        /// <summary>손에 조인트로 매달린 아이템 (0=빈손). 소형은 Slots[Selected]와 같고, 대형은 슬롯 밖.</summary>
        public NetworkVariable<ulong> CarriedItemNetId = new NetworkVariable<ulong>(0);
        public NetworkList<ulong> Slots = new NetworkList<ulong>();
        public NetworkVariable<int> SelectedSlot = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private InputAction _grabAction, _throwAction;
        private PlayerController _movement;
        private Rigidbody _rb;
        private PlayerUpgrades _upgrades;

        private bool _charging;
        private float _chargeStart;
        private LineRenderer _trajectoryLine;

        // 대형 앵커: 몸 수직축 위 (루트 스케일 공간). 앞뒤 오프셋이 없어야 시선 따라 몸이 돌아도 물건이 안 휘둘린다 —
        // 물건과의 간격은 아이템 쪽 자리(CarrySlot)가 갖는다
        private static readonly Vector3 HeavyAnchorLocal = new Vector3(0f, 0.3f, 0f);

        // 자동 대형: 자리 배정 받으면 소유 클라가 짧게 미끄러져 붙는다
        private bool _snapping;
        private float _snapT;
        private Vector3 _snapFrom, _snapTo;
        private Collider[] _snapIgnored;

        private void Awake()
        {
            _movement = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
            _upgrades = GetComponent<PlayerUpgrades>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerEnsureSlots(_balance.BaseCarrySlots);
                SelectedSlot.OnValueChanged += OnSelectedSlotChangedServer;
            }
            if (!IsOwner) return;
            var map = _inputAsset.FindActionMap("Player", true);
            _grabAction = map.FindAction("Grab", true);
            _throwAction = map.FindAction("Throw", true);
            _grabAction.performed += OnGrabPressed;
            _throwAction.started += OnThrowStarted;
            _throwAction.canceled += OnThrowReleased;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) SelectedSlot.OnValueChanged -= OnSelectedSlotChangedServer;
            if (IsServer) ServerReleaseOnLeave();
            if (!IsOwner) return;
            _grabAction.performed -= OnGrabPressed;
            _throwAction.started -= OnThrowStarted;
            _throwAction.canceled -= OnThrowReleased;
        }

        // ---- UI용 읽기 전용 ----
        public CarryableItem GrabCandidate { get; private set; }
        public bool IsHolding => CarriedItemNetId.Value != 0;
        public bool IsDraggingHeavy { get { var it = GetCarriedItem(); return it != null && it.IsHeavy; } }
        public CarryableItem CarriedItem => GetCarriedItem();
        public float ThrowCharge => _charging ? Mathf.Clamp01((Time.time - _chargeStart) / _balance.ThrowChargeTime) : 0f;
        public CarryableItem GetSlotItem(int slot) =>
            slot < Slots.Count && Slots[slot] != 0 ? Resolve(Slots[slot]) : null;

        private void Update()
        {
            if (!IsOwner) return;
            ApplyCarryPenalty();
            GrabCandidate = IsHolding ? null : FindGrabCandidate();
            if (_charging) UpdateTrajectoryPreview();
            ReadSlotInput();
        }

        /// <summary>무인 테스트용 — 소유 클라에서 들고 있는 걸 던지기(차지 0~1, 시선 방향)·내려놓기. 실제 입력과 같은 RPC (고양이 236).</summary>
        public void DevThrow(float charge)
        {
            if (!IsOwner || !IsHolding) return;
            ThrowRequestServerRpc(GetThrowDirection(), Mathf.Clamp01(charge));
        }

        public void DevPutDown()
        {
            if (!IsOwner || !IsHolding) return;
            PutDownRequestServerRpc();
        }

        /// <summary>무인 테스트용 — 소유 클라에서 가장 가까운 물건 집기 (커서 잠금 무시).</summary>
        public void DevGrabNearest()
        {
            if (!IsOwner || IsHolding) return;
            var item = FindGrabCandidate();
            if (item != null) GrabRequestServerRpc(item.NetworkObjectId);
        }

        // ---- 소유 클라: 입력 ----

        private void ReadSlotInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked || IsDraggingHeavy) return;
            int want = SelectedSlot.Value;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) want = 0;
                if (kb.digit2Key.wasPressedThisFrame) want = 1;
                if (kb.digit3Key.wasPressedThisFrame) want = 2;
                if (kb.digit4Key.wasPressedThisFrame) want = 3;
            }
            if (want >= SlotCount) want = SelectedSlot.Value; // 아직 안 산 칸
            // 마우스 휠 전환은 끔 (2026-09-13 사용자 요청 — 테스트 중 휠 오작동). 1·2 키만
            if (want != SelectedSlot.Value) { CancelCharge(); SelectedSlot.Value = want; }
        }

        private void OnGrabPressed(InputAction.CallbackContext ctx)
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (_charging) { CancelCharge(); return; }
            if (IsHolding) { PutDownRequestServerRpc(); return; }
            var item = FindGrabCandidate();
            if (item != null) GrabRequestServerRpc(item.NetworkObjectId);
        }

        private void OnThrowStarted(InputAction.CallbackContext ctx)
        {
            if (Cursor.lockState != CursorLockMode.Locked || !IsHolding || IsDraggingHeavy) return;
            _charging = true;
            _chargeStart = Time.time;
        }

        private void CancelCharge()
        {
            _charging = false;
            HideTrajectory();
        }

        private void OnThrowReleased(InputAction.CallbackContext ctx)
        {
            if (!_charging) return;
            _charging = false;
            HideTrajectory();
            if (!IsHolding) return;
            ThrowRequestServerRpc(GetThrowDirection(), Mathf.Clamp01((Time.time - _chargeStart) / _balance.ThrowChargeTime));
        }

        // 1인칭: 조준점을 향해 던진다 (위아래 조준 포함). 손이 화면 오른쪽 아래에 있어 시선과 평행하게 던지면
        // 손 오프셋(약 0.45m)만큼 옆에 떨어진다 — 시선이 닿는 곳(최소~최대 거리로 제한)으로 모은다
        private Vector3 GetThrowDirection()
        {
            var cam = Camera.main;
            if (cam == null || _handAnchor == null) return (transform.forward + Vector3.up * 0.15f).normalized;
            Vector3 origin = cam.transform.position, forward = cam.transform.forward;
            int mask = ~LayerMask.GetMask("Player", "Carryable", "Ragdoll");
            float dist = Physics.Raycast(origin, forward, out var hit, _balance.ThrowAimMaxDistance, mask, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(hit.distance, _balance.ThrowAimMinDistance)
                : _balance.ThrowAimMaxDistance;
            Vector3 aimDir = (origin + forward * dist - _handAnchor.position).normalized;
            return (aimDir + Vector3.up * 0.15f).normalized;
        }

        private void UpdateTrajectoryPreview()
        {
            var item = GetCarriedItem();
            if (item == null) { HideTrajectory(); return; }
            if (_trajectoryLine == null)
            {
                var go = new GameObject("ThrowTrajectory");
                go.transform.SetParent(transform, false);
                _trajectoryLine = go.AddComponent<LineRenderer>();
                _trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
                // 시작점이 눈앞 0.4m라 굵기가 같으면 화면을 가르는 흰 띠가 된다 — 손 쪽을 가늘게
                _trajectoryLine.startWidth = 0.008f;
                _trajectoryLine.endWidth = 0.05f;
                _trajectoryLine.positionCount = 10;
                _trajectoryLine.useWorldSpace = true;
            }
            _trajectoryLine.enabled = true;
            float v0 = _balance.GetThrowSpeed(ThrowCharge, item.Mass) * (_upgrades != null ? _upgrades.ThrowPowerMultiplier : 1f);
            Vector3 vel = GetThrowDirection() * v0;
            Vector3 pos = _handAnchor != null ? _handAnchor.position : transform.position;
            // 처음 부딪히는 곳에서 끊는다 — 안 끊으면 바닥 아래로 이어진 점들이 화면 아래로 휘어 갈고리처럼 보인다
            int mask = ~LayerMask.GetMask("Player", "Carryable", "Ragdoll");
            Vector3 prev = pos;
            int count = 10;
            _trajectoryLine.positionCount = 10;
            _trajectoryLine.SetPosition(0, pos);
            for (int i = 1; i < 10; i++)
            {
                float t = i * 0.12f;
                Vector3 p = pos + vel * t + 0.5f * Physics.gravity * t * t;
                if (Physics.Linecast(prev, p, out var hit, mask, QueryTriggerInteraction.Ignore))
                {
                    _trajectoryLine.SetPosition(i, hit.point);
                    count = i + 1;
                    break;
                }
                _trajectoryLine.SetPosition(i, p);
                prev = p;
            }
            _trajectoryLine.positionCount = count;
        }

        private void HideTrajectory()
        {
            if (_trajectoryLine != null) _trajectoryLine.enabled = false;
        }

        // 주변 GrabRange 안에서 가장 가까운 물건 (주머니 속 물건은 콜라이더가 꺼져 있어 안 잡힘)
        private CarryableItem FindGrabCandidate()
        {
            int mask = LayerMask.GetMask("Carryable");
            var overlaps = Physics.OverlapSphere(transform.position, _balance.GrabRange, mask, QueryTriggerInteraction.Ignore);
            CarryableItem best = null;
            float bestDist = float.MaxValue;
            foreach (var col in overlaps)
            {
                var item = col.GetComponentInParent<CarryableItem>();
                if (item == null || item.Pocketed.Value) continue;
                float d = Vector3.Distance(item.transform.position, transform.position);
                if (d < bestDist) { bestDist = d; best = item; }
            }
            return best;
        }

        // ---- 호스트: 실행 ---- PlayerCarryController.Server.cs (고양이 204)

        // ---- 소유 클라: 무게 → 이동 페널티 (docs/05) ----
        private void ApplyCarryPenalty()
        {
            var item = GetCarriedItem();
            if (item == null)
            {
                _movement.CarrySpeedMultiplier = 1f;
                _movement.BlockSprint = _movement.BlockJump = false;
                return;
            }
            float loadPerRat = item.Mass / Mathf.Max(1, item.CarrierIds.Count);
            _movement.CarrySpeedMultiplier = _balance.GetCarrySpeedMultiplier(loadPerRat);
            _movement.BlockSprint = loadPerRat > _balance.SprintBlockLoad;
            _movement.BlockJump = loadPerRat > _balance.JumpBlockLoad;
        }
    }
}
