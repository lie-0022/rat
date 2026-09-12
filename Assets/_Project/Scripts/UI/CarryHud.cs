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

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _carry = GetComponent<PlayerCarryController>();
        }

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

            // 조준점: 십자 (가운데 비움)
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 1, cy - 9, 2, 6), _dot);
            GUI.DrawTexture(new Rect(cx - 1, cy + 3, 2, 6), _dot);
            GUI.DrawTexture(new Rect(cx - 9, cy - 1, 6, 2), _dot);
            GUI.DrawTexture(new Rect(cx + 3, cy - 1, 6, 2), _dot);
            GUI.color = Color.white;

            // 프롬프트
            string prompt = null;
            if (_carry.IsHolding)
            {
                prompt = _carry.ThrowCharge > 0f
                    ? $"던지기 {Mathf.RoundToInt(_carry.ThrowCharge * 100)}%"
                    : "[좌클릭] 내려놓기   [우클릭 홀드] 던지기";
            }
            else if (_carry.GrabCandidate != null)
            {
                string name = _carry.GrabCandidate.Data != null
                    ? _carry.GrabCandidate.Data.DisplayName : _carry.GrabCandidate.name;
                prompt = $"[좌클릭] 집기 — {name}";
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
