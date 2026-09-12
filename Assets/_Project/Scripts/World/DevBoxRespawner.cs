using System.Collections;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 샌드박스 전용: 납품돼 사라진 박스를 잠시 뒤 제자리에 다시 스폰 — 집기→운반→두기 루프를 반복 테스트하기 위함.
    /// 호스트에서만. 정식 스폰 테이블(태스크 2-1)이 생기면 삭제.
    /// </summary>
    public class DevBoxRespawner : MonoBehaviour
    {
        [SerializeField] private GameObject _boxPrefab;
        [SerializeField] private Vector3 _spawnPosition = new Vector3(-3f, 1f, 6f);
        [SerializeField] private float _delay = 1.5f;

        private void OnEnable() => EventBus.LootDeposited += OnDeposited;
        private void OnDisable() => EventBus.LootDeposited -= OnDeposited;

        private void OnDeposited(LootItemSO item, int value)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || _boxPrefab == null) return;
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(_delay);
            var go = Instantiate(_boxPrefab, _spawnPosition, Quaternion.identity);
            go.name = _boxPrefab.name;
            go.GetComponent<NetworkObject>().Spawn(true);
            Log.Dev("[Sandbox] 박스 리스폰");
        }
    }
}
