using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// E 상호작용 (docs/04). 소유 클라: SphereCast(r 0.3, d 1.2)로 IInteractable 탐지, 홀드 시간 채우면
    /// 요청 → 호스트가 CanInteract 재검증 후 ServerInteract. HUD 프롬프트·진행 게이지는 UI 태스크(2-6),
    /// 우선순위 정렬(구출>함정>문>자판기)은 대상 종류가 늘어나는 2-2에서 — 지금은 첫 히트.
    /// </summary>
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction _interactAction;
        private NetworkBehaviour _holdTarget; // IInteractable이기도 한 대상
        private float _holdStartTime;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            _interactAction = _inputAsset.FindActionMap("Player", true).FindAction("Interact", true);
        }

        private void Update()
        {
            if (!IsOwner || _interactAction == null) return;

            if (!_interactAction.IsPressed() || InputFocus.IsUiOpen) // 메뉴가 떠 있으면 E로 또 열지 않게
            {
                _holdTarget = null;
                return;
            }

            if (_holdTarget == null)
            {
                _holdTarget = FindTarget();
                _holdStartTime = Time.time;
                if (_holdTarget != null)
                    Log.Dev($"상호작용 시작: {((IInteractable)_holdTarget).PromptText}");
                return;
            }

            var interactable = (IInteractable)_holdTarget;
            if (Time.time - _holdStartTime >= interactable.HoldSeconds)
            {
                InteractRequestServerRpc(_holdTarget.NetworkObjectId);
                _holdTarget = null;
            }
        }

        private NetworkBehaviour FindTarget()
        {
            var cam = Camera.main;
            Vector3 origin = transform.position;
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;

            // 전방 캐스트 + 근접 폴백 (무인 테스트·등 뒤 관용 — Carry와 동일 패턴)
            if (Physics.SphereCast(origin, 0.3f, dir, out var hit, 1.2f, ~0, QueryTriggerInteraction.Collide))
            {
                var target = FromCollider(hit.collider);
                if (target != null) return target;
            }
            foreach (var col in Physics.OverlapSphere(origin, 1.2f, ~0, QueryTriggerInteraction.Collide))
            {
                var target = FromCollider(col);
                if (target != null) return target;
            }
            return null;
        }

        private NetworkBehaviour FromCollider(Collider col)
        {
            foreach (var nb in col.GetComponentsInParent<NetworkBehaviour>())
                if (nb is IInteractable ia && nb != this && ia.CanInteract(OwnerClientId))
                    return nb;
            return null;
        }

        [ServerRpc]
        private void InteractRequestServerRpc(ulong targetNetId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var netObj)) return;
            foreach (var nb in netObj.GetComponents<NetworkBehaviour>())
            {
                if (nb is not IInteractable interactable) continue;
                if (Vector3.Distance(netObj.transform.position, transform.position) > 2.5f) return; // 관용치
                if (!interactable.CanInteract(OwnerClientId)) return;
                interactable.ServerInteract(OwnerClientId);
                return;
            }
        }
    }
}
