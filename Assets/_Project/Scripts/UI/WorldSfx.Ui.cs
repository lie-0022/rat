using System.Collections.Generic;
using RatGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RatGame.UI
{
    /// <summary>
    /// UI 클릭 소리 (고양이 256) — 버튼마다 붙이지 않고, 왼쪽 클릭이 눌린 순간 포인터 아래 첫 UI가 켜진 Selectable(버튼·토글·슬라이더…)이면 톡.
    /// 나중에 생기는 창(상점·도감·설정)도 따로 할 일 없이 들린다.
    /// </summary>
    public partial class WorldSfx
    {
        private readonly List<RaycastResult> _uiHits = new();
        private int _uiClicks; // 시험용

        private void UpdateUiClick()
        {
            var mouse = Mouse.current;
            var es = EventSystem.current;
            if (_audio == null || mouse == null || es == null || !mouse.leftButton.wasPressedThisFrame) return;
            var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            if (_uiHits.Count == 0) return;
            var sel = _uiHits[0].gameObject.GetComponentInParent<Selectable>();
            if (sel == null || !sel.IsInteractable()) return;
            _ui.PlayOneShot(_audio.UiClickClip, _audio.UiClickVolume * Sfx);
            _uiClicks++;
        }
    }
}
