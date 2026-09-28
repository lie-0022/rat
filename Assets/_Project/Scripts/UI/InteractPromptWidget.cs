using RatGame.Player;
using RatGame.Run;
using RatGame.World;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 조준점 아래 집기·놓기·던지기 안내 (docs/12 InteractPrompt 중 운반 부분). PlayerCarryController 상태만 읽는다.
    /// 상자는 글자 길이에 맞춰 늘어난다 (HorizontalLayoutGroup + ContentSizeFitter).
    /// </summary>
    public class InteractPromptWidget : MonoBehaviour
    {
        [SerializeField] private GameObject _box;
        [SerializeField] private TMP_Text _text;

        private PlayerCarryController _carry;

        public void Bind(PlayerCarryController carry) => _carry = carry;

        private void Update()
        {
            string prompt = _carry != null ? BuildPrompt(_carry) : null;
            bool show = prompt != null;
            if (_box.activeSelf != show) _box.SetActive(show);
            if (show && _text.text != prompt) _text.text = prompt;
        }

        private static string BuildPrompt(PlayerCarryController carry)
        {
            if (carry.IsHolding)
            {
                if (carry.IsDraggingHeavy)
                {
                    var held = carry.CarriedItem;
                    if (held == null) return "[좌클릭] 놓기";
                    if (held.GetComponent<DownedBody>() != null) // 어디로 끌어야 살아나는지 (고양이 185)
                        return $"[좌클릭] 놓기   {ReviveSpot()}으로 끌고 가면 살아나요 {held.CarrierIds.Count}/{held.CarrySlotCount}";
                    return $"[좌클릭] 놓기   함께 드는 중 {held.CarrierIds.Count}/{held.CarrySlotCount}";
                }
                return carry.ThrowCharge > 0f
                    ? "[우클릭 떼기] 던지기   [좌클릭] 취소"
                    : "[좌클릭] 내려놓기   [우클릭 홀드] 던지기";
            }

            var cand = carry.GrabCandidate;
            if (cand == null) return null;
            string name = cand.Data != null ? cand.Data.DisplayName : cand.name;
            var body = cand.GetComponent<DownedBody>();
            if (body != null && body.Owner.Value != ulong.MaxValue) name = $"쓰러진 {PlayerVisual.ColorNameFor(body.Owner.Value)}"; // 누구 몸인지 (고양이 185)
            if (!cand.IsHeavy) return $"[좌클릭] 집기 — {name}";
            if (cand.IsCarrySlotsFull) return $"{name} — 자리 없음 ({cand.CarrierIds.Count}/{cand.CarrySlotCount})";
            return $"[좌클릭] 같이 들기 — {name} ({cand.CarrierIds.Count}/{cand.CarrySlotCount})";
        }

        // 벽 속(할당량 있는 새 루프)은 창고 방이 살리는 자리
        private static string ReviveSpot() =>
            RunManager.Instance != null && RunManager.Instance.GetComponent<StageQuota>() != null ? "창고(주황 방)" : "쥐구멍";
    }
}
