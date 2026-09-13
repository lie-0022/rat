using RatGame.Player;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 조준점 + 집기 프롬프트 + 인벤 슬롯 + 결과 패널 (소유 클라 로컬 IMGUI). 시스템 상태를 읽기만 한다 — 호출 없음 (CLAUDE.md 규칙 3).
    /// 상단 런 표시는 uGUI RunBar로 옮겼다 — 여기서 생성·파괴만 한다.
    /// 정식 HUD(태스크 2-6)가 생기면 나머지도 그쪽으로 흡수.
    /// </summary>
    public class CarryHud : NetworkBehaviour
    {
        [SerializeField] private RunBar _runBarPrefab;

        private PlayerCarryController _carry;
        private RunBar _runBar;
        private Texture2D _dot;
        private GUIStyle _promptStyle;
        private GUIStyle _bigStyle;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _carry = GetComponent<PlayerCarryController>();
            if (_runBarPrefab != null)
            {
                // 플레이어는 씬 전환에도 살아남으므로 바도 같이 유지
                _runBar = Instantiate(_runBarPrefab);
                DontDestroyOnLoad(_runBar.gameObject);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_runBar != null) Destroy(_runBar.gameObject);
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
                _bigStyle = new GUIStyle(_promptStyle) { fontSize = 28 };
            }

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;

            // 결과 화면 중엔 조작 UI(슬롯·조준점·게이지·프롬프트)를 숨긴다 — 결과 패널 위에 겹쳐 그려져 글자를 가림
            var runNow = RunManager.Instance;
            if (runNow != null && runNow.IsShowingResult)
            {
                if (runNow.Phase.Value == RunPhase.Returned) DrawReturnedPanel(runNow, cx, cy);
                else DrawWipedPanel(runNow, cx, cy);
                return;
            }

            // 인벤 슬롯 (하단 중앙) — 선택 슬롯 = 손에 든 것. 대형 끌기 중엔 흐리게
            const float slotW = 150f, slotH = 40f, gap = 8f;
            float totalW = PlayerCarryController.SlotCount * slotW + (PlayerCarryController.SlotCount - 1) * gap;
            for (int i = 0; i < PlayerCarryController.SlotCount; i++)
            {
                var r = new Rect(cx - totalW * 0.5f + i * (slotW + gap), Screen.height - slotH - 20, slotW, slotH);
                bool selected = _carry.SelectedSlot.Value == i;
                GUI.color = selected ? new Color(1f, 0.9f, 0.4f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(r, _dot);
                if (selected) { GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), _dot); }
                GUI.color = _carry.IsDraggingHeavy ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
                var item = _carry.GetSlotItem(i);
                string label = item != null ? (item.Data != null ? item.Data.DisplayName : item.name) : "—";
                GUI.Label(r, $"{i + 1}  {label}", _promptStyle);
            }
            GUI.color = Color.white;

            // 조준점: 십자 (가운데 비움)
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 1, cy - 9, 2, 6), _dot);
            GUI.DrawTexture(new Rect(cx - 1, cy + 3, 2, 6), _dot);
            GUI.DrawTexture(new Rect(cx - 9, cy - 1, 6, 2), _dot);
            GUI.DrawTexture(new Rect(cx + 3, cy - 1, 6, 2), _dot);
            GUI.color = Color.white;

            // 던지기 차지 게이지: 조준점 오른쪽 고정, 아래서 위로 참.
            // 1인칭이라 머리 월드 위치는 카메라와 겹쳐 화면 밖으로 투영된다 — 화면 좌표로 그린다
            if (_carry.ThrowCharge > 0f)
            {
                const float w = 14f, h = 70f;
                float gx = cx + 40f;
                float gy = cy - h * 0.5f;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(gx - 2, gy - 2, w + 4, h + 4), _dot);
                float fill = h * _carry.ThrowCharge;
                GUI.color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.2f), _carry.ThrowCharge);
                GUI.DrawTexture(new Rect(gx, gy + (h - fill), w, fill), _dot);
                GUI.color = Color.white;
            }

            // 프롬프트
            string prompt = null;
            if (_carry.IsHolding)
            {
                if (_carry.IsDraggingHeavy)
                {
                    var held = _carry.CarriedItem;
                    prompt = held != null
                        ? $"[좌클릭] 놓기   함께 드는 중 {held.CarrierIds.Count}/{held.CarrySlotCount}"
                        : "[좌클릭] 놓기";
                }
                else if (_carry.ThrowCharge > 0f) prompt = "[우클릭 떼기] 던지기   [좌클릭] 취소";
                else prompt = "[좌클릭] 내려놓기   [우클릭 홀드] 던지기";
            }
            else if (_carry.GrabCandidate != null)
            {
                var cand = _carry.GrabCandidate;
                string name = cand.Data != null ? cand.Data.DisplayName : cand.name;
                if (!cand.IsHeavy) prompt = $"[좌클릭] 집기 — {name}";
                else if (cand.IsCarrySlotsFull) prompt = $"{name} — 자리 없음 ({cand.CarrierIds.Count}/{cand.CarrySlotCount})";
                else prompt = $"[좌클릭] 같이 들기 — {name} ({cand.CarrierIds.Count}/{cand.CarrySlotCount})";
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

        private void DrawReturnedPanel(RunManager run, float cx, float cy)
        {
            int stashed = run.StashedValue.Value, carried = run.ResultCarriedValue.Value;
            double remain = System.Math.Max(0.0, run.ResultEndsAt.Value - NetworkManager.ServerTime.Time);
            var panel = new Rect(cx - 260, cy - 140, 520, 250);
            GUI.color = new Color(0.1f, 0.3f, 0.15f, 0.9f);
            GUI.DrawTexture(panel, _dot);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 14, panel.width, 44), "귀환!", _bigStyle);
            GUI.Label(new Rect(panel.x, panel.y + 70, panel.width, 30), $"쥐구멍 {stashed}  +  들고 온 것 {carried}", _promptStyle);
            GUI.Label(new Rect(panel.x, panel.y + 102, panel.width, 30), $"이번 수확  {stashed + carried}", _promptStyle);
            GUI.Label(new Rect(panel.x, panel.y + 134, panel.width, 30), $"누계  {run.RunTotalValue.Value}", _promptStyle);
            GUI.Label(new Rect(panel.x, panel.y + 200, panel.width, 30), $"{remain:0}초 뒤 기지로", _promptStyle);
        }

        private void DrawWipedPanel(RunManager run, float cx, float cy)
        {
            double remain = System.Math.Max(0.0, run.ResultEndsAt.Value - NetworkManager.ServerTime.Time);
            var panel = new Rect(cx - 260, cy - 140, 520, 250);
            GUI.color = new Color(0.35f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(panel, _dot);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 14, panel.width, 44), "전멸…", _bigStyle);
            GUI.Label(new Rect(panel.x, panel.y + 70, panel.width, 30), "이번 파밍분을 잃었다", _promptStyle);
            GUI.Label(new Rect(panel.x, panel.y + 102, panel.width, 30), $"누계  {run.RunTotalValue.Value}  (유지)", _promptStyle);
            GUI.Label(new Rect(panel.x, panel.y + 134, panel.width, 30), $"잃은 쥐구멍 적립  {run.StashedValue.Value}", _promptStyle);
            GUI.Label(new Rect(panel.x, panel.y + 200, panel.width, 30), $"{remain:0}초 뒤 기지로", _promptStyle);
        }
    }
}
