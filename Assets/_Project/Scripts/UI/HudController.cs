using RatGame.Core;
using RatGame.Player;
using RatGame.Run;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 인게임 HUD 루트 (docs/12 HudController). 한 캔버스 아래 RunBar·스태미나 링·조준점·집기 안내·던지기 게이지·인벤 슬롯을 둔다.
    /// 소유 플레이어의 CarryHud가 생성·Bind·파괴한다. 시스템 상태는 읽기만 한다 (규칙 3).
    /// 결과 화면 중엔 조작 UI(조준점·안내·게이지·슬롯)를 숨기고, 메뉴 패널이 열려 있으면 조준점·안내·게이지만 숨긴다.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private StaminaRing _staminaRing;
        [SerializeField] private CanvasGroup _aimGroup;   // 조준점·집기 안내·던지기 게이지
        [SerializeField] private CanvasGroup _slotsGroup; // 인벤 슬롯
        [SerializeField] private InventorySlotsWidget _slots;
        [SerializeField] private InteractPromptWidget _prompt;
        [SerializeField] private ThrowGaugeWidget _gauge;
        [SerializeField] private HoldGaugeWidget _holdGauge; // [E] 홀드 (구출)
        [SerializeField] private CarryInfoWidget _carryInfo; // 손에 든 물건 카드 (슬롯 그룹 안)

        public void Bind(PlayerCarryController carry, PlayerStamina stamina, PlayerInteractor interactor)
        {
            if (_staminaRing != null) _staminaRing.Bind(stamina);
            _slots.Bind(carry);
            _prompt.Bind(carry);
            _gauge.Bind(carry);
            if (_holdGauge != null) _holdGauge.Bind(interactor);
            if (_carryInfo != null) _carryInfo.Bind(carry);
        }

        private void Update()
        {
            var run = RunManager.Instance;
            bool result = run != null && run.IsShowingResult;
            SetVisible(_aimGroup, !result && !InputFocus.IsUiOpen);
            SetVisible(_slotsGroup, !result);
        }

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            float alpha = visible ? 1f : 0f;
            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;
        }
    }
}
