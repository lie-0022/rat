using System.Text;
using RatGame.AI;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 고양이 디버그 오버레이 (2026-09-24, 개발 도구 — production/plans/cat-25). F3로 켜고 끈다(기본 꺼짐).
    /// 오른쪽 위 목록: 고양이마다 성격·상태(실패 종류·잠 단계)·게이지·타깃·찍힌 쥐·예민·숨김. 호스트면 디렉터·관계·집주인 이벤트도.
    /// 고양이 머리 위에 "상태 · 게이지" 라벨. 읽기만 한다(규칙 3). IMGUI는 DEV 전용 예외(docs/12 — DEV 넷 패널과 같음).
    /// 에디터·개발 빌드에서만 자동 생성된다.
    /// </summary>
    public class CatDebugOverlay : MonoBehaviour
    {
        private bool _visible;
        private GUIStyle _box, _label;
        private readonly StringBuilder _sb = new();
        private CatBrain[] _cats = new CatBrain[0];
        private float _nextScan;

        public bool Visible => _visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            var go = new GameObject("CatDebugOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<CatDebugOverlay>();
        }

        public void SetVisible(bool on) => _visible = on;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) _visible = !_visible;
            if (_visible && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                _cats = FindObjectsByType<CatBrain>(FindObjectsSortMode.None);
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            _box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, wordWrap = false };
            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, richText = true };

            _sb.Clear();
            _sb.Append("<b>고양이 디버그 (F3)</b>\n");
            var nm = NetworkManager.Singleton;
            bool host = nm != null && nm.IsServer;
            var run = RunManager.Instance;
            if (host && run != null)
            {
                var dir = run.GetComponent<RunDirector>();
                var rel = run.GetComponent<CatRelation>();
                var house = run.GetComponent<HouseEventDirector>();
                if (dir != null) _sb.Append($"디렉터 {dir.Mode} · 긴장 {dir.Tension:0}\n");
                if (rel != null && _cats.Length > 1) _sb.Append($"관계 {rel.Kind} · 싸움 {rel.FightsStarted}\n");
                if (house != null) _sb.Append($"집주인 남은 {house.Remaining}건\n");
            }

            var cam = Camera.main;
            foreach (var cat in _cats)
            {
                if (cat == null || !cat.IsSpawned) continue;
                string state = StateText(cat);
                var senses = cat.GetComponent<CatSenses>();
                float gauge = senses != null ? senses.SuspicionGauge.Value : 0f;
                _sb.Append($"\n<b>{cat.name}</b> {(cat.Personality != null ? cat.Personality.DisplayName : "기본")}\n");
                _sb.Append($"  {state} · 게이지 {gauge:0}");
                if (cat.State.Value == CatState.Chase) _sb.Append($" · 타깃 {cat.TargetClientId.Value}");
                _sb.Append('\n');
                if (cat.HasGrudge.Value) _sb.Append($"  찍힌 쥐 {cat.GrudgeClientId.Value}\n");
                if (cat.Alert.Value) _sb.Append("  예민(디렉터)\n");
                if (cat.AwayHidden.Value) _sb.Append("  문 밖(부재)\n");
                if (cat.AmbushHidden.Value) _sb.Append("  매복(꼬리만)\n");

                // 머리 위 라벨
                if (cam == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(cat.transform.position + Vector3.up * 1.6f);
                if (sp.z <= 0f) continue;
                var r = new Rect(sp.x - 90f, Screen.height - sp.y - 12f, 180f, 24f);
                GUI.Label(r, $"<b>{state}</b> · {gauge:0}", _label);
            }

            var content = new GUIContent(_sb.ToString());
            Vector2 size = _box.CalcSize(content);
            GUI.Box(new Rect(Screen.width - size.x - 20f, 20f, size.x + 10f, size.y + 6f), content, _box);
        }

        private static string StateText(CatBrain cat)
        {
            var st = cat.State.Value;
            if (st == CatState.Blunder) return $"실패:{cat.BlunderKind.Value}";
            if (st == CatState.Sleep) return $"잠:{cat.SleepPhase.Value}";
            return st.ToString();
        }
    }
}
