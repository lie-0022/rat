using System.Collections.Generic;
using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 전체 아이템 목록 SO (docs/08). Id→SO 조회 — NetworkVariable에는 Id 해시(int)만 실어 보낸다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "RatGame/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<LootItemSO> _items = new();

        private Dictionary<int, LootItemSO> _byHash;

        public IReadOnlyList<LootItemSO> Items => _items;

        public static int HashId(string id) => Animator.StringToHash(id); // 결정적 해시

        public LootItemSO GetByHash(int idHash)
        {
            if (_byHash == null)
            {
                _byHash = new Dictionary<int, LootItemSO>();
                foreach (var item in _items)
                    if (item != null) _byHash[HashId(item.Id)] = item;
            }
            return _byHash.TryGetValue(idHash, out var so) ? so : null;
        }

        public LootItemSO GetById(string id) => GetByHash(HashId(id));

#if UNITY_EDITOR
        public void EditorSetItems(List<LootItemSO> items) { _items = items; _byHash = null; }
#endif
    }
}
