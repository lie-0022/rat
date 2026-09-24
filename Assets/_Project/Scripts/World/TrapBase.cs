using System.Collections.Generic;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 함정 공통 (docs/10 함정 테이블, 2026-09-24 고양이 49). 호스트가 0.1s마다 박스 안의 Active 쥐·풀린 물건을 찾아 파생에 넘긴다.
    /// 트리거 대신 폴링 — 원격 쥐는 호스트에서 kinematic 사본이라 트리거 판정이 흔들린다(WaterBowl과 같은 방식).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public abstract class TrapBase : NetworkBehaviour
    {
        [SerializeField] protected BalanceConfigSO _balance;

        private BoxCollider _box;
        private float _nextPoll;
        private CarryableItem[] _items;
        private float _nextItemScan;

        protected virtual void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true; // 몸은 부딪히지 않는다 — 밟고 지나가는 판
        }

        protected virtual void Update()
        {
            if (!IsServer || Time.time < _nextPoll) return;
            _nextPoll = Time.time + 0.1f;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var po = client.PlayerObject;
                if (po == null) continue;
                var cond = po.GetComponent<PlayerCondition>();
                if (cond == null || cond.State.Value != ConditionState.Active) continue;
                if (Contains(po.transform.position, 1.2f)) OnRat(cond);
            }
            if (!WantsItems) return;
            if (Time.time >= _nextItemScan || _items == null)
            {
                _nextItemScan = Time.time + 1f;
                _items = FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            }
            foreach (var item in _items)
            {
                if (item == null || !item.IsSpawned || item.CarrierIds.Count > 0 || item.Pocketed.Value) continue;
                if (Contains(item.transform.position, 0.8f)) OnItem(item);
            }
        }

        /// <summary>XZ는 박스 안, 높이는 박스 윗면에서 maxAbove 안 (쥐 중심은 바닥+0.65).</summary>
        protected bool Contains(Vector3 worldPos, float maxAbove)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos) - _box.center;
            Vector3 half = _box.size * 0.5f;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.z) return false;
            float top = transform.TransformPoint(_box.center + Vector3.up * half.y).y;
            return worldPos.y <= top + maxAbove && worldPos.y >= transform.TransformPoint(_box.center - Vector3.up * half.y).y - 0.2f;
        }

        protected virtual bool WantsItems => false;
        protected abstract void OnRat(PlayerCondition rat);
        protected virtual void OnItem(CarryableItem item) { }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
