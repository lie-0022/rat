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
            _grabAction.canceled += OnGrabReleased;
            _throwAction.performed += OnThrowPressed;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            _grabAction.performed -= OnGrabPressed;
            _grabAction.canceled -= OnGrabReleased;
            _throwAction.performed -= OnThrowPressed;
        }

        private void Update()
        {
            if (!IsOwner) return;
            ApplyCarryPenalty();
        }

        // ---- 소유 클라: 입력 → 요청 ----

        private void OnGrabPressed(InputAction.CallbackContext ctx)
        {
            if (CarriedItemNetId.Value != 0) return; // 한 번에 한 아이템 (docs/05)
            var item = FindGrabCandidate();
            if (item != null) GrabRequestServerRpc(item.NetworkObjectId);
        }

        private void OnGrabReleased(InputAction.CallbackContext ctx)
        {
            if (CarriedItemNetId.Value != 0) ReleaseRequestServerRpc();
        }

        private void OnThrowPressed(InputAction.CallbackContext ctx)
        {
            if (CarriedItemNetId.Value == 0) return;
            var cam = Camera.main;
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            dir = (dir + Vector3.up * 0.3f).normalized; // 살짝 위로 — 손맛
            ThrowRequestServerRpc(dir);
        }

        private CarryableItem FindGrabCandidate()
        {
            // 카메라 전방 SphereCast (docs/05: r 0.35, d 1.0, Carryable 레이어)
            var cam = Camera.main;
            Vector3 origin = _handAnchor != null ? _handAnchor.position : transform.position;
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            int mask = LayerMask.GetMask("Carryable");

            if (Physics.SphereCast(origin, 0.35f, dir, out var hit, 1.0f, mask, QueryTriggerInteraction.Ignore))
                return hit.rigidbody != null ? hit.rigidbody.GetComponent<CarryableItem>() : null;
            // 배회/근접 폴백: 주변 반경 탐색 (자동 테스트와 등 뒤 아이템 관용)
            var overlaps = Physics.OverlapSphere(origin, 0.9f, mask, QueryTriggerInteraction.Ignore);
            return overlaps.Length > 0 ? overlaps[0].GetComponentInParent<CarryableItem>() : null;
        }

        // ---- 호스트: 검증 + 실행 ----

        [ServerRpc]
        private void GrabRequestServerRpc(ulong itemNetId)
        {
            if (CarriedItemNetId.Value != 0) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
            var item = netObj.GetComponent<CarryableItem>();
            if (item == null) return;
            if (Vector3.Distance(item.transform.position, transform.position) > _balance.GrabRange + 1f) return;

            Vector3 handLocal = _handAnchor != null ? _handAnchor.localPosition : new Vector3(0f, 0f, 0.6f);
            if (item.ServerTryGrab(OwnerClientId, _rb, handLocal))
                CarriedItemNetId.Value = itemNetId;
        }

        [ServerRpc]
        private void ReleaseRequestServerRpc()
        {
            var item = GetCarriedItem();
            item?.ServerRelease(OwnerClientId);
            CarriedItemNetId.Value = 0;
        }

        [ServerRpc]
        private void ThrowRequestServerRpc(Vector3 dir)
        {
            var item = GetCarriedItem();
            item?.ServerRelease(OwnerClientId, thrown: true, throwDir: dir);
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
