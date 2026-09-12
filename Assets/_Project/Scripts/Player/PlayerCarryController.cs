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
    public class PlayerCarryController : NetworkBehaviour
    {
        public const int SlotCount = 2; // 기본 2칸 — 상점 확장은 메타(2-4)에서

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

        private bool _charging;
        private float _chargeStart;
        private LineRenderer _trajectoryLine;

        // 대형 앵커: 손보다 더 앞 (아이템 몸통이 내 콜라이더와 안 겹치게)
        private static readonly Vector3 HeavyAnchorLocal = new Vector3(0f, 0.3f, 2.0f);

        private void Awake()
        {
            _movement = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                while (Slots.Count < SlotCount) Slots.Add(0);
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
            if (!IsOwner) return;
            _grabAction.performed -= OnGrabPressed;
            _throwAction.started -= OnThrowStarted;
            _throwAction.canceled -= OnThrowReleased;
        }

        // ---- UI용 읽기 전용 ----
        public CarryableItem GrabCandidate { get; private set; }
        public bool IsHolding => CarriedItemNetId.Value != 0;
        public bool IsDraggingHeavy { get { var it = GetCarriedItem(); return it != null && it.IsHeavy; } }
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
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.01f) want = (want + SlotCount - 1) % SlotCount;
                else if (scroll < -0.01f) want = (want + 1) % SlotCount;
            }
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

        // 1인칭: 시선 방향으로 던진다 (위아래 조준 포함)
        private Vector3 GetThrowDirection()
        {
            var cam = Camera.main;
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            return (dir + Vector3.up * 0.15f).normalized;
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
                _trajectoryLine.startWidth = _trajectoryLine.endWidth = 0.05f;
                _trajectoryLine.positionCount = 10;
                _trajectoryLine.useWorldSpace = true;
            }
            _trajectoryLine.enabled = true;
            float v0 = _balance.GetThrowSpeed(ThrowCharge, item.Mass);
            Vector3 vel = GetThrowDirection() * v0;
            Vector3 pos = _handAnchor != null ? _handAnchor.position : transform.position;
            for (int i = 0; i < 10; i++)
            {
                float t = i * 0.12f;
                _trajectoryLine.SetPosition(i, pos + vel * t + 0.5f * Physics.gravity * t * t);
            }
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

        // ---- 호스트: 실행 ----

        private Vector3 HandLocal => _handAnchor != null ? _handAnchor.localPosition : new Vector3(0.35f, 0.15f, 1.0f);

        [ServerRpc]
        private void GrabRequestServerRpc(ulong itemNetId)
        {
            if (IsHolding) return;
            var condition = GetComponent<PlayerCondition>();
            if (condition != null && condition.State.Value != ConditionState.Active) return;
            var item = Resolve(itemNetId);
            if (item == null || item.Pocketed.Value) return;
            if (Vector3.Distance(item.transform.position, transform.position) > _balance.GrabRange + 1f) return;

            if (item.IsHeavy)
            {
                if (item.ServerTryGrab(OwnerClientId, _rb, HeavyAnchorLocal)) CarriedItemNetId.Value = itemNetId;
                return;
            }

            // 소형: 선택 슬롯이 비었으면 거기, 아니면 빈 슬롯 → 주머니
            int target = Slots[SelectedSlot.Value] == 0 ? SelectedSlot.Value : FindEmptySlot();
            if (target < 0) return; // 슬롯 가득 참
            if (target == SelectedSlot.Value)
            {
                if (!item.ServerTryGrab(OwnerClientId, _rb, HandLocal)) return;
                CarriedItemNetId.Value = itemNetId;
            }
            else
            {
                item.ServerPocket(OwnerClientId, transform);
            }
            Slots[target] = itemNetId;
        }

        // 슬롯 전환(소유 클라가 값 변경) → 서버가 손 교체: 이전 손 물건 주머니로, 새 슬롯 물건 손으로
        private void OnSelectedSlotChangedServer(int prev, int now)
        {
            if (IsDraggingHeavy) return;
            var prevItem = prev < Slots.Count ? Resolve(Slots[prev]) : null;
            if (prevItem != null && CarriedItemNetId.Value == prevItem.NetworkObjectId)
                prevItem.ServerPocket(OwnerClientId, transform);
            CarriedItemNetId.Value = 0;

            var nextItem = now < Slots.Count ? Resolve(Slots[now]) : null;
            if (nextItem == null) return;
            nextItem.ServerUnpocket(transform.TransformPoint(HandLocal));
            if (nextItem.ServerTryGrab(OwnerClientId, _rb, HandLocal)) CarriedItemNetId.Value = nextItem.NetworkObjectId;
        }

        [ServerRpc]
        private void PutDownRequestServerRpc()
        {
            var item = GetCarriedItem();
            if (item != null && item.IsHeavy)
            {
                item.ServerRelease(OwnerClientId);
            }
            else if (item != null)
            {
                float halfHeight = item.GetComponent<Collider>().bounds.extents.y;
                Vector3 pos = transform.position + transform.forward * 0.7f;
                pos.y = transform.position.y - transform.localScale.y + halfHeight + 0.05f;
                item.ServerPutDown(OwnerClientId, pos);
                ClearSlotOf(item.NetworkObjectId);
            }
            CarriedItemNetId.Value = 0;
        }

        [ServerRpc]
        private void ThrowRequestServerRpc(Vector3 dir, float charge)
        {
            var item = GetCarriedItem();
            if (item == null) return;
            item.ServerRelease(OwnerClientId, thrown: true, throwDir: dir, charge: Mathf.Clamp01(charge));
            ClearSlotOf(item.NetworkObjectId);
            CarriedItemNetId.Value = 0;
        }

        /// <summary>호스트 전용 — 다운·속박·접속 해제 시 손·주머니 전부 드랍 (docs/04·05).</summary>
        public void ServerForceDrop()
        {
            if (!IsServer) return;
            GetCarriedItem()?.ServerRelease(OwnerClientId);
            CarriedItemNetId.Value = 0;
            for (int i = 0; i < Slots.Count; i++)
            {
                var item = Resolve(Slots[i]);
                if (item != null && item.Pocketed.Value)
                    item.ServerUnpocket(transform.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f);
                Slots[i] = 0;
            }
        }

        public float CurrentLoadPerRat
        {
            get
            {
                var item = GetCarriedItem();
                return item == null ? 0f : item.Mass / Mathf.Max(1, item.CarrierIds.Count);
            }
        }

        // 끊김(거리 초과 등)으로 아이템 쪽에서 해제됐을 때 손·슬롯 정리
        private void FixedUpdate()
        {
            if (!IsServer || CarriedItemNetId.Value == 0) return;
            var item = GetCarriedItem();
            if (item == null || !item.CarrierIds.Contains(OwnerClientId))
            {
                ClearSlotOf(CarriedItemNetId.Value);
                CarriedItemNetId.Value = 0;
            }
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < Slots.Count; i++) if (Slots[i] == 0) return i;
            return -1;
        }

        private void ClearSlotOf(ulong netId)
        {
            for (int i = 0; i < Slots.Count; i++) if (Slots[i] == netId) Slots[i] = 0;
        }

        private CarryableItem GetCarriedItem() => CarriedItemNetId.Value == 0 ? null : Resolve(CarriedItemNetId.Value);

        private CarryableItem Resolve(ulong netId) =>
            netId != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(netId, out var obj)
                ? obj.GetComponent<CarryableItem>() : null;

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
