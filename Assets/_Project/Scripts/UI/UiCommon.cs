using RatGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RatGame.UI
{
    /// <summary>패널 공통 — EventSystem 보장. 커서·입력 잠금은 Core/InputFocus.</summary>
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
    }
}
