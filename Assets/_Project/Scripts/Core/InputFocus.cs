using UnityEngine;

namespace RatGame.Core
{
    /// <summary>
    /// 메뉴 패널이 떠 있는지 + 이번 프레임 Esc를 패널이 먹었는지. UI 패널이 쓰고 플레이어 입력(이동·시선·상호작용)이 읽는다.
    /// Player가 UI 타입을 직접 참조하지 않게 Core에 둔다 (CLAUDE.md 규칙 3).
    /// </summary>
    public static class InputFocus
    {
        private static int _openPanels;
        private static int _escConsumedFrame = -1;

        public static bool IsUiOpen => _openPanels > 0;

        /// <summary>패널이 Esc로 닫힌 프레임 — 시선 리그가 같은 Esc로 커서를 다시 풀지 않게.</summary>
        public static bool EscConsumedThisFrame => _escConsumedFrame == Time.frameCount;

        /// <summary>커서 재잠금 클릭을 무시할 화면 영역 (DEV IMGUI 패널 — 버튼 클릭이 잠금으로 먹히지 않게). GUI 좌표(좌상단 원점).</summary>
        public static Rect NoRelockGuiRect { get; set; }

        public static void PanelOpened()
        {
            _openPanels++;
            ApplyCursor();
        }

        public static void PanelClosed(bool byEscape)
        {
            _openPanels = Mathf.Max(0, _openPanels - 1);
            if (byEscape) _escConsumedFrame = Time.frameCount;
            ApplyCursor();
        }

        private static void ApplyCursor()
        {
            bool free = IsUiOpen;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _openPanels = 0;
            _escConsumedFrame = -1;
        }
    }
}
