using System.Collections;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 샌드박스 전용: 납품돼 사라진 전리품을 잠시 뒤 제자리에 다시 스폰 — 집기→운반→두기 루프 반복 테스트용.
    /// 자기가 맡은 아이템(_lootData)이 납품됐을 때만 반응한다. 호스트 전용.
    /// 정식 스폰 테이블(태스크 2-1)이 생기면 삭제.
    /// </summary>
    public class DevBoxRespawner : MonoBehaviour
    {
        [SerializeField] private LootItemSO _lootData;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector3 _spawnPosition;
        [SerializeField] private float _delay = 1.5f;

        private void OnEnable() => EventBus.LootDeposited += OnDeposited;
        private void OnDisable() => EventBus.LootDeposited -= OnDeposited;

        private void OnDeposited(LootItemSO item, int value)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || _prefab == null) return;
            if (item != _lootData) return; // 내 담당 아이템만
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(_delay);
            var go = Instantiate(_prefab, _spawnPosition, Quaternion.identity);
            go.name = _prefab.name;
            go.GetComponent<NetworkObject>().Spawn(true);
            Log.Dev($"[Sandbox] {_prefab.name} 리스폰");
        }
    }
}
