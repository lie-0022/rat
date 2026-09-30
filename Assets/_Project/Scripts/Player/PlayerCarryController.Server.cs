using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>운반 — 호스트 실행(잡기·놓기·던지기 요청 처리, 슬롯, 손 조인트 유지). PlayerCarryController.cs에서 옮김 (고양이 204).</summary>
    public partial class PlayerCarryController
    {
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
                // 자동 대형: 서버가 자리 배정 → 소유 클라에게 "거기로 붙어라" 통보 (혼자 잡아도 동일)
                if (!item.ServerTryGrabSlot(OwnerClientId, _rb, HeavyAnchorLocal, out Vector3 stand)) return;
                CarriedItemNetId.Value = itemNetId;
                SnapToSlotClientRpc(stand, itemNetId, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
                });
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
            item.ServerRelease(OwnerClientId, thrown: true, throwDir: dir, charge: Mathf.Clamp01(charge),
                throwPower: _upgrades != null ? _upgrades.ThrowPowerMultiplier : 1f); // 상점 업그레이드 (docs/11)
            ClearSlotOf(item.NetworkObjectId);
            CarriedItemNetId.Value = 0;
        }

        /// <summary>
        /// 호스트: 쥐가 사라질 때(클라 이탈) 든 물건·주머니 물건을 풀어 둔다 (docs/03 접속 해제, 고양이 264).
        /// 없으면 나간 쥐의 캐리어가 물건에 남아 영영 적립이 안 되고(들고 있는 걸로 봄), 주머니 물건은 안 보인 채 사라졌다.
        /// 사라지는 중이라 내 NV는 안 쓰고 물건 쪽만 고친다. 세션을 통째로 끌 땐 물건도 같이 사라지니 건너뜀.
        /// </summary>
        private void ServerReleaseOnLeave()
        {
            var nm = NetworkManager;
            if (nm == null || nm.ShutdownInProgress) return;
            var held = GetCarriedItem();
            if (held != null && held.IsSpawned) held.ServerRelease(OwnerClientId);
            for (int i = 0; i < Slots.Count; i++)
            {
                var item = Resolve(Slots[i]);
                if (item != null && item.IsSpawned && item.Pocketed.Value)
                    item.ServerUnpocket(transform.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f);
            }
            Log.Dev($"이탈한 쥐의 물건 풀기: client {OwnerClientId}");
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

        // 소유 클라: 배정된 자리로 이동. 반대편 자리면 물건을 가로질러야 해서 이동 중엔 그 물건과 충돌을 끈다
        [ClientRpc]
        private void SnapToSlotClientRpc(Vector3 standWorld, ulong itemNetId, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            _snapFrom = _rb.position;
            _snapTo = new Vector3(standWorld.x, _rb.position.y, standWorld.z);
            _snapT = 0f;
            _snapping = true;

            var item = Resolve(itemNetId);
            _snapIgnored = item != null ? item.GetComponentsInChildren<Collider>() : null;
            SetIgnoreItemCollision(true);
        }

        private void SetIgnoreItemCollision(bool ignore)
        {
            if (_snapIgnored == null) return;
            foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var theirs in _snapIgnored)
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, ignore);
        }

        // 끊김(거리 초과 등)으로 아이템 쪽에서 해제됐을 때 손·슬롯 정리
        private void FixedUpdate()
        {
            if (IsOwner && _snapping)
            {
                _snapT += Time.fixedDeltaTime / Mathf.Max(_balance.CarrySlotSnapTime, 0.01f);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_snapT));
                _rb.MovePosition(Vector3.Lerp(_snapFrom, _snapTo, t));
                if (_snapT >= 1f)
                {
                    _snapping = false;
                    SetIgnoreItemCollision(false);
                    _snapIgnored = null;
                }
            }

            if (!IsServer || CarriedItemNetId.Value == 0) return;
            var item = GetCarriedItem();
            if (item == null || !item.CarrierIds.Contains(OwnerClientId))
            {
                ClearSlotOf(CarriedItemNetId.Value);
                CarriedItemNetId.Value = 0;
            }
        }

        /// <summary>호스트: 칸을 count까지 늘린다 (줄이지 않는다 — 든 물건이 사라지지 않게).</summary>
        public void ServerEnsureSlots(int count)
        {
            if (!IsServer) return;
            while (Slots.Count < count) Slots.Add(0);
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < Slots.Count; i++) if (Slots[i] == 0) return i;
            return -1;
        }

        /// <summary>호스트: 손에 든 이 물건을 없앤다(먹기 — World/EdibleItem). 디스폰은 호출자가.</summary>
        public bool ServerConsumeCarried(CarryableItem item)
        {
            if (!IsServer || item == null || CarriedItemNetId.Value != item.NetworkObjectId) return false;
            item.ServerRelease(OwnerClientId);
            ClearSlotOf(item.NetworkObjectId);
            CarriedItemNetId.Value = 0;
            return true;
        }

        private void ClearSlotOf(ulong netId)
        {
            for (int i = 0; i < Slots.Count; i++) if (Slots[i] == netId) Slots[i] = 0;
        }

        private CarryableItem GetCarriedItem() => CarriedItemNetId.Value == 0 ? null : Resolve(CarriedItemNetId.Value);

        private CarryableItem Resolve(ulong netId) =>
            netId != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(netId, out var obj)
                ? obj.GetComponent<CarryableItem>() : null;
    }
}
