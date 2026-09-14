using RatGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RatGame.UI
{
    /// <summary>패널 공통 — EventSystem 보장, 커서 잠금/해제 (시선·클릭 정지는 lockState를 보는 쪽이 알아서).</summary>
    public static class UiCommon
    {
        /// <summary>씬에 EventSystem이 없으면 버튼이 조용히 안 눌린다 (ui-ugui 스킬 규칙) — 정확히 하나만.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
            Log.Dev("EventSystem 생성");
        }

        public static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
    }
}
