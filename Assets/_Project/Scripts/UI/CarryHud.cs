using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 조준점 + 집기 프롬프트 (소유 클라 로컬 IMGUI). 시스템 상태를 읽기만 한다 — 호출 없음 (CLAUDE.md 규칙 3).
    /// 정식 HUD(태스크 2-6)가 생기면 그쪽으로 흡수.
    /// </summary>
    public class CarryHud : NetworkBehaviour
    {
        private PlayerCarryController _carry;
        private Texture2D _dot;
        private GUIStyle _promptStyle;

        private int _deposited; // 이 세션 납품 수 (로컬 표시용 — 정식 할당량 HUD는 2-6)

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _carry = GetComponent<PlayerCarryController>();
            Core.EventBus.LootDeposited += OnLootDeposited;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) Core.EventBus.LootDeposited -= OnLootDeposited;
        }

        private void OnLootDeposited(Data.LootItemSO item, int value) => _deposited++;

        private void OnGUI()
        {
            if (_carry == null) return;
            if (_dot == null)
            {
                _dot = new Texture2D(1, 1);
                _dot.SetPixel(0, 0, Color.white);
                _dot.Apply();
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                _promptStyle.normal.textColor = Color.white;
            }

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;

            // 납품 카운트 (상단 중앙)
            var countRect = new Rect(cx - 80, 12, 160, 30);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(countRect, _dot);
            GUI.color = Color.white;
            GUI.Label(countRect, $"납품 {_deposited}", _promptStyle);

            // 조준점: 십자 (가운데 비움)
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 1, cy - 9, 2, 6), _dot);
            GUI.DrawTexture(new Rect(cx - 1, cy + 3, 2, 6), _dot);
            GUI.DrawTexture(new Rect(cx - 9, cy - 1, 6, 2), _dot);
            GUI.DrawTexture(new Rect(cx + 3, cy - 1, 6, 2), _dot);
            GUI.color = Color.white;

            // 던지기 차지 게이지: 머리 약간 오른쪽, 아래서 위로 참
            if (_carry.ThrowCharge > 0f && Camera.main != null)
            {
                var head = transform.Find("HeadAnchor");
                Vector3 headWorld = head != null ? head.position : transform.position + Vector3.up * transform.localScale.y;
                Vector3 sp = Camera.main.WorldToScreenPoint(headWorld);
                if (sp.z > 0f)
                {
                    const float w = 14f, h = 70f;
                    float gx = sp.x + 45f;
                    float gy = Screen.height - sp.y - h * 0.5f;
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(gx - 2, gy - 2, w + 4, h + 4), _dot);
                    float fill = h * _carry.ThrowCharge;
                    GUI.color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.2f), _carry.ThrowCharge);
                    GUI.DrawTexture(new Rect(gx, gy + (h - fill), w, fill), _dot);
                    GUI.color = Color.white;
                }
            }

            // 프롬프트
            string prompt = null;
            if (_carry.IsHolding)
            {
                if (_carry.IsDraggingHeavy) prompt = "[좌클릭] 놓기   (무거워서 던질 수 없음)";
                else if (_carry.ThrowCharge > 0f) prompt = "[우클릭 떼기] 던지기   [좌클릭] 취소";
                else prompt = "[좌클릭] 내려놓기   [우클릭 홀드] 던지기";
            }
            else if (_carry.GrabCandidate != null)
            {
                var cand = _carry.GrabCandidate;
                string name = cand.Data != null ? cand.Data.DisplayName : cand.name;
                prompt = cand.IsHeavy ? $"[좌클릭] 끌기 — {name} (대형)" : $"[좌클릭] 집기 — {name}";
            }
            if (prompt != null)
            {
                var rect = new Rect(cx - 220, cy + 30, 440, 30);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(rect, _dot);
                GUI.color = Color.white;
                GUI.Label(rect, prompt, _promptStyle);
            }
        }
    }
}
