using System.Collections.Generic;
using RatGame.Player;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 하단 중앙 인벤 슬롯 (uGUI). 칸 수는 SlotCount(상점 주머니 확장으로 2~4)를 따라 템플릿을 늘린다.
    /// 선택 슬롯 = 손에 든 것. 대형 끌기 중엔 전체를 흐리게 (슬롯 전환 불가 표시).
    /// </summary>
    public class InventorySlotsWidget : MonoBehaviour
    {
        [SerializeField] private InventorySlotView _slotTemplate; // 비활성 템플릿
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Range(0f, 1f)] private float _draggingAlpha = 0.4f;

        private PlayerCarryController _carry;
        private readonly List<InventorySlotView> _views = new();

        public void Bind(PlayerCarryController carry) => _carry = carry;

        private void Update()
        {
            if (_carry == null) return;

            int count = _carry.SlotCount;
            while (_views.Count < count)
            {
                var view = Instantiate(_slotTemplate, _slotTemplate.transform.parent);
                view.gameObject.SetActive(true);
                _views.Add(view);
            }
            while (_views.Count > count)
            {
                Destroy(_views[^1].gameObject);
                _views.RemoveAt(_views.Count - 1);
            }

            int selected = _carry.SelectedSlot.Value;
            for (int i = 0; i < _views.Count; i++)
            {
                var item = _carry.GetSlotItem(i);
                string label = item != null ? (item.Data != null ? item.Data.DisplayName : item.name) : "—";
                _views[i].Set(i + 1, label, i == selected);
            }

            float alpha = _carry.IsDraggingHeavy ? _draggingAlpha : 1f;
            if (!Mathf.Approximately(_group.alpha, alpha)) _group.alpha = alpha;
        }
    }
}
