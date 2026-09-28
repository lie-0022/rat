using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 킁킁 (docs/04 Sniff, 고양이 76). R → 목적지 쪽 다음 문까지 냄새 줄기를 내 화면에만.
    /// 소음·판정이 없는 표시라 소유 클라에서 끝난다(동기화 없음). 줄기 그리기는 UI가 EventBus로 받는다(규칙 3).
    /// </summary>
    public class PlayerSniff : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction _sniffAction;
        private float _lastSniff = float.NegativeInfinity;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner || _inputAsset == null) return;
            _sniffAction = _inputAsset.FindActionMap("Player", true).FindAction("Sniff", true);
        }

        private void Update()
        {
            if (!IsOwner || _sniffAction == null) return;
            if (_sniffAction.WasPressedThisFrame() && !InputFocus.IsUiOpen) TrySniff();
        }

        /// <summary>소유 클라: 쿨다운이 지났으면 냄새 줄기. DevRemoteControl "sniff"도 이 경로.</summary>
        public void TrySniff()
        {
            if (!IsOwner) return;
            float left = _balance.SniffCooldownSeconds - (Time.unscaledTime - _lastSniff);
            if (left > 0f) { EventBus.RaiseSniffCooldown(left); return; } // 키가 안 먹은 것처럼 보이지 않게 (고양이 162)
            Vector3 from = transform.position;
            if (!GridRoute.TryNextHint(from, out var target, out int roomsLeft))
            {
                Log.Dev("킁킁: 냄새가 안 남 (목적지 없음)");
                return;
            }
            _lastSniff = Time.unscaledTime;
            EventBus.RaiseSniffHint(from, target, _balance.SniffTrailSeconds);
            Log.Dev($"킁킁: client {OwnerClientId} 다음 목표 {target:F1}까지 {Vector3.Distance(from, target):F1}m, 목적지까지 방 {roomsLeft}칸");
            SniffFood(from);
        }

        private readonly List<(float d, Vector3 p, int v)> _food = new();
        private readonly List<Vector3> _spots = new();
        private readonly List<int> _tiers = new();

        // 넓어진 방에서 드문 음식 찾기 (고양이 150) — 가까운 음식 몇 개에서 김이 피어오른다. 표시일 뿐이라 로컬
        private void SniffFood(Vector3 from)
        {
            _food.Clear();
            float max = _balance.SniffFoodMeters * _balance.SniffFoodMeters;
            foreach (var item in FindObjectsByType<World.CarryableItem>(FindObjectsSortMode.None))
            {
                if (!item.IsSpawned || item.Pocketed.Value || item.EffectiveValue <= 0) continue; // 쓰러진 몸(가치 0)·유인물·상점 물건 빼고
                float d = (item.transform.position - from).sqrMagnitude;
                if (d <= max) _food.Add((d, item.transform.position, item.EffectiveValue));
            }
            if (_food.Count == 0) { Log.Dev("킁킁 음식: 0개"); return; }
            _food.Sort((a, b) => a.d.CompareTo(b.d));
            _spots.Clear(); _tiers.Clear();
            var cut = _balance.SniffFoodTierValues; int t0 = 0, t1 = 0, t2 = 0;
            for (int i = 0; i < _food.Count && i < _balance.SniffFoodMax; i++)
            {
                int v = _food[i].v, tier = v < cut.x ? 0 : v < cut.y ? 1 : 2; // 비쌀수록 진한 냄새 (고양이 160)
                _spots.Add(_food[i].p); _tiers.Add(tier);
                if (tier == 0) t0++; else if (tier == 1) t1++; else t2++;
            }
            EventBus.RaiseSniffFood(_spots, _tiers, _balance.SniffTrailSeconds);
            Log.Dev($"킁킁 음식: {_spots.Count}개 (가장 가까운 {Mathf.Sqrt(_food[0].d):F1}m, 옅음 {t0}·보통 {t1}·진함 {t2})");
        }
    }
}
