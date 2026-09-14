using RatGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace RatGame.UI
{
    /// <summary>패널 공통 — EventSystem 보장, 닫기 키. 커서·입력 잠금은 Core/InputFocus.</summary>
    public static class UiCommon
    {
        // 열자마자 같은 E 입력으로 닫히지 않게
        private const float CloseGraceSeconds = 0.2f;

        /// <summary>씬에 EventSystem이 없으면 버튼이 조용히 안 눌린다 (ui-ugui 스킬 규칙) — 정확히 하나만.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
            Log.Dev("EventSystem 생성");
        }

        /// <summary>
        /// 패널 닫기 키: Esc 또는 E(상호작용 키).
        /// 에디터 Game 뷰는 Esc를 누르면 커서 잠금을 스스로 풀고, 코드로 다시 잠가도 Game 뷰를 클릭하기 전엔 안 잠긴다 —
        /// E로 닫으면 에디터에서도 바로 조작이 이어진다 (빌드는 Esc도 잠금이 유지됨).
        /// </summary>
        public static bool ClosePressed(float openedAt, out bool byEscape)
        {
            byEscape = false;
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (kb.escapeKey.wasPressedThisFrame) { byEscape = true; return true; }
            return kb.eKey.wasPressedThisFrame && Time.unscaledTime - openedAt > CloseGraceSeconds;
        }
    }
}
