using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 잡기·놓기·던지기 (docs/05). 소유 클라가 후보를 찾아 요청 → 호스트 검증·조인트 생성.
    /// 무게 페널티는 소유 클라에서 PlayerController에 반영 (docs/05 공식).
    /// GripPoint 선택은 문서와 달리 호스트가 한다(가장 가까운 빈 grip) — 클라 선택+검증보다
    /// 단순하고 어차피 서버 권위. docs/05 수정 제안 사항.
    /// 던지기 차지(0→1 홀드)는 태스크 1-1 — 지금은 고정 0.5.
    /// </summary>
    public class PlayerCarryController : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private Transform _handAnchor;

        public NetworkVariable<ulong> CarriedItemNetId = new NetworkVariable<ulong>(0); // 0=빈손

        private InputAction _grabAction, _throwAction;
        private PlayerController _movement;
        private Rigidbody _rb;

        // 던지기 차지 (docs/05: 홀드 0→1, 만충 1.2s. 궤적 프리뷰는 소유 클라 로컬만)
        private bool _charging;
        private float _chargeStart;
        private LineRenderer _trajectoryLine;

        private void Awake()
        {
            _movement = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
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
            if (!IsOwner) return;
            _grabAction.performed -= OnGrabPressed;
            _throwAction.started -= OnThrowStarted;
            _throwAction.canceled -= OnThrowReleased;
        }

        /// <summary>UI용 읽기 전용 상태 (소유 클라). 집을 수 있는 후보 / 던지기 차지 진행도.</summary>
        public CarryableItem GrabCandidate { get; private set; }
        public bool IsHolding => CarriedItemNetId.Value != 0;
        /// <summary>들고 있는 게 대형(끌기 모드)인가.</summary>
        public bool IsDraggingHeavy { get { var it = GetCarriedItem(); return it != null && it.IsHeavy; } }

        // 대형 끌기 앵커: 몸 "뒤" 낮은 위치 (루트 스케일 공간). 쥐는 이동 방향을 보므로 앞에 두면
        // 자기 박스에 막혀 못 간다 — 썰매처럼 뒤에 매달아 끈다
        private static readonly Vector3 DragAnchorLocal = new Vector3(0f, 0.3f, -1.3f);
        public float ThrowCharge => _charging ? Mathf.Clamp01((Time.time - _chargeStart) / _balance.ThrowChargeTime) : 0f;

        private void Update()
        {
            if (!IsOwner) return;
            ApplyCarryPenalty();
            GrabCandidate = IsHolding ? null : FindGrabCandidate();
            if (_charging) UpdateTrajectoryPreview();
        }

        // ---- 소유 클라: 입력 → 요청 ----

        // 토글: 빈손이면 집기, 들고 있으면 발 앞에 살짝 내려놓기 (던지기는 우클릭 별도)
        private void OnGrabPressed(InputAction.CallbackContext ctx)
        {
            if (Cursor.lockState != CursorLockMode.Locked) return; // 커서 풀림 = 메뉴 조작 중
            if (_charging) { CancelCharge(); return; } // 차지 중 좌클릭 = 던지기 취소 (물건은 계속 들고 있음)
            if (CarriedItemNetId.Value != 0)
            {
                PutDownRequestServerRpc();
                return;
            }
            var item = FindGrabCandidate();
            if (item != null) GrabRequestServerRpc(item.NetworkObjectId);
        }

        private void OnThrowStarted(InputAction.CallbackContext ctx)
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (CarriedItemNetId.Value == 0) return;
            if (IsDraggingHeavy) return; // 대형은 못 던진다
            _charging = true;
            _chargeStart = Time.time;
        }

        /// <summary>차지 중단 — 던지지 않고 계속 들고 있는다.</summary>
        private void CancelCharge()
        {
            _charging = false;
            HideTrajectory();
        }

        private void OnThrowReleased(InputAction.CallbackContext ctx)
        {
            if (!_charging) return; // 이미 취소됐으면 던지지 않는다
            _charging = false;
            HideTrajectory();
            if (CarriedItemNetId.Value == 0) return;
            float charge = Mathf.Clamp01((Time.time - _chargeStart) / _balance.ThrowChargeTime);
            ThrowRequestServerRpc(GetThrowDirection(), charge);
        }

        // 1인칭: 시선 방향으로 던진다 (위아래 조준 포함). 카메라 없으면 몸 정면
        private Vector3 GetThrowDirection()
        {
            var cam = Camera.main;
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            return (dir + Vector3.up * 0.15f).normalized;
        }

        // 궤적 프리뷰: 소유 클라 로컬 전용, 점 10개 (docs/05)
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

            float charge = Mathf.Clamp01((Time.time - _chargeStart) / _balance.ThrowChargeTime);
            float v0 = _balance.GetThrowSpeed(charge, item.Mass);
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

        // 주변 GrabRange 안에서 가장 가까운 물건 하나 — 방향 안 따짐 (토글 조작에 맞춘 단순 규칙)
        private CarryableItem FindGrabCandidate()
        {
            int mask = LayerMask.GetMask("Carryable");
            var overlaps = Physics.OverlapSphere(transform.position, _balance.GrabRange, mask, QueryTriggerInteraction.Ignore);
            CarryableItem best = null;
            float bestDist = float.MaxValue;
            foreach (var col in overlaps)
            {
                var item = col.GetComponentInParent<CarryableItem>();
                if (item == null) continue;
                float d = Vector3.Distance(item.transform.position, transform.position);
                if (d < bestDist) { bestDist = d; best = item; }
            }
            return best;
        }

        // ---- 호스트: 검증 + 실행 ----

        [ServerRpc]
        private void GrabRequestServerRpc(ulong itemNetId)
        {
            if (CarriedItemNetId.Value != 0) return;
            var condition = GetComponent<PlayerCondition>();
            if (condition != null && condition.State.Value != ConditionState.Active) return; // docs/05 검증 3
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
            var item = netObj.GetComponent<CarryableItem>();
            if (item == null) return;
            if (Vector3.Distance(item.transform.position, transform.position) > _balance.GrabRange + 1f) return;

            Vector3 handLocal;
            if (item.IsHeavy)
            {
                handLocal = DragAnchorLocal; // 대형: 앞에서 끌기
            }
            else
            {
                // 머리 위 앵커 + 아이템 반높이만큼 더 올림 — 아이템 바닥이 머리 캡슐과 안 겹치게
                handLocal = _handAnchor != null ? _handAnchor.localPosition : new Vector3(0f, 1.3f, 0f);
                float itemHalf = item.GetComponent<Collider>().bounds.extents.y;
                handLocal.y += (itemHalf + 0.1f) / Mathf.Max(transform.localScale.y, 0.01f); // 앵커는 루트 스케일 공간
            }
            if (item.ServerTryGrab(OwnerClientId, _rb, handLocal))
                CarriedItemNetId.Value = itemNetId;
        }

        // 내려놓기: 발 앞 0.7m, 속도 0 — 머리 위에서 떨어뜨리는 게 아니라 "두는" 동작
        [ServerRpc]
        private void PutDownRequestServerRpc()
        {
            var item = GetCarriedItem();
            if (item != null && item.IsHeavy)
            {
                item.ServerRelease(OwnerClientId); // 대형은 이미 바닥에 있음 — 그냥 놓기
            }
            else if (item != null)
            {
                float halfHeight = item.GetComponent<Collider>().bounds.extents.y;
                Vector3 pos = transform.position + transform.forward * 0.7f;
                pos.y = transform.position.y - transform.localScale.y + halfHeight + 0.05f; // 발밑 기준
                item.ServerPutDown(OwnerClientId, pos);
            }
            CarriedItemNetId.Value = 0;
        }

        /// <summary>호스트 전용 — 다운·속박·접속 해제 시 강제 드랍 (docs/04·05).</summary>
        public void ServerForceDrop()
        {
            if (!IsServer || CarriedItemNetId.Value == 0) return;
            GetCarriedItem()?.ServerRelease(OwnerClientId);
            CarriedItemNetId.Value = 0;
        }

        /// <summary>현재 1인당 하중 (스태미나 소모 판정용 — docs/04).</summary>
        public float CurrentLoadPerRat
        {
            get
            {
                var item = GetCarriedItem();
                if (item == null) return 0f;
                return item.Mass / Mathf.Max(1, item.CarrierIds.Count);
            }
        }

        [ServerRpc]
        private void ThrowRequestServerRpc(Vector3 dir, float charge)
        {
            var item = GetCarriedItem();
            item?.ServerRelease(OwnerClientId, thrown: true, throwDir: dir, charge: Mathf.Clamp01(charge));
            CarriedItemNetId.Value = 0;
        }

        /// <summary>호스트 전용 — 끊김(거리 초과 등)으로 아이템 쪽에서 해제됐을 때 상태 정리.</summary>
        private void FixedUpdate()
        {
            if (!IsServer || CarriedItemNetId.Value == 0) return;
            var item = GetCarriedItem();
            if (item == null || !item.CarrierIds.Contains(OwnerClientId))
                CarriedItemNetId.Value = 0;
        }

        private CarryableItem GetCarriedItem()
        {
            if (CarriedItemNetId.Value == 0) return null;
            return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(CarriedItemNetId.Value, out var obj)
                ? obj.GetComponent<CarryableItem>() : null;
        }

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
            int carriers = Mathf.Max(1, item.CarrierIds.Count);
            float loadPerRat = item.Mass / carriers;
            _movement.CarrySpeedMultiplier = _balance.GetCarrySpeedMultiplier(loadPerRat);
            _movement.BlockSprint = loadPerRat > _balance.SprintBlockLoad;
            _movement.BlockJump = loadPerRat > _balance.JumpBlockLoad;
        }
    }
}
