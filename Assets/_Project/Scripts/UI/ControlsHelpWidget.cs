using System.Text;
using RatGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.UI
{
    /// <summary>
    /// 조작 안내 (HUD, 고양이 77). 평소엔 왼쪽 위 작은 "F1 조작" 꼬리표, F1로 키 목록을 펼친다.
    /// 키 이름은 입력 에셋 바인딩에서 읽는다 — 키를 바꿔도 안내가 따라가게. 조작을 막지 않는 안내라 입력 포커스는 안 잡는다.
    /// </summary>
    public class ControlsHelpWidget : MonoBehaviour
    {
        // (액션, 표시 이름) — 입력 에셋 Player 맵
        private static readonly (string action, string label)[] Rows =
        {
            ("Move", "이동"), ("Sprint", "달리기"), ("Crouch", "웅크리기"), ("Jump", "점프"),
            ("Grab", "잡기 · 놓기"), ("Throw", "던지기 (누르고 있다 떼기)"), ("Interact", "상호작용 ([E] 안내가 뜨면)"),
            ("Squeak", "찍찍 (소리 남 · 쓰러져서도 됨)"), ("Ping", "핑"), ("Sniff", "킁킁 — 목적지·음식 냄새"),
        };

        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private GameObject _tag;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _list;
        [SerializeField] private TMP_Text _title; // 키 글자색 = 제목 강조색 (테마를 따라가게)
        [SerializeField] private RectTransform _below; // 팀 상자 — 인원 수만큼 길어져서 그 아래를 따라간다 (고양이 83)
        private const float Gap = 10f;

        private bool _open;

        private void Start()
        {
            Show(false);
            Loc.Changed += OnLanguageChanged;
        }

        private void OnDestroy() => Loc.Changed -= OnLanguageChanged;

        // 펼친 채로 언어를 바꿔도 목록이 따라가게 (고양이 190)
        private void OnLanguageChanged()
        {
            if (_open) _list.text = BuildList();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) Show(!_open);
            bool hide = InputFocus.IsUiOpen; // 메뉴 패널 위로 겹치지 않게
            if (_tag.activeSelf == (hide || _open)) _tag.SetActive(!hide && !_open);
            if (_panel.activeSelf != (_open && !hide)) _panel.SetActive(_open && !hide);
        }

        // 팀 상자 바닥 아래로 (둘 다 좌상단 기준)
        private void LateUpdate()
        {
            if (_below == null) return;
            float y = _below.anchoredPosition.y - _below.rect.height - Gap;
            Follow((RectTransform)_tag.transform, y);
            Follow((RectTransform)_panel.transform, y);
        }

        private static void Follow(RectTransform rt, float y)
        {
            if (Mathf.Abs(rt.anchoredPosition.y - y) > 0.5f) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        private void Show(bool open)
        {
            _open = open;
            if (open) _list.text = BuildList();
        }

        private string BuildList()
        {
            var sb = new StringBuilder();
            string key = _title != null ? "#" + ColorUtility.ToHtmlStringRGB(_title.color) : "#FFFFFF";
            var map = _inputAsset != null ? _inputAsset.FindActionMap("Player") : null;
            foreach (var (action, label) in Rows)
            {
                var a = map?.FindAction(action);
                if (a == null) continue;
                Line(sb, key, Keys(a), Loc.T(label));
            }
            Line(sb, key, "1~4", Loc.T("주머니 칸 고르기"));
            Line(sb, key, "Tab", Loc.T("지도 (벽 속, 누르고 있기)"));
            Line(sb, key, "Esc", Loc.T("메뉴"));
            // 화면 읽기 — 3번째 루프에서 늘어난 표시들 (고양이 176)
            sb.Append('\n').Append("<color=").Append(key).Append(">").Append(Loc.T("화면 읽기")).Append("</color>\n");
            sb.Append(Loc.T("고양이 위 <b>?</b> 의심(밑에 이유) · <b>!</b> 추격")).Append('\n');
            sb.Append(Loc.T("오른쪽 아래 — 내 소리 크기 · 냄새 남기는 중")).Append('\n');
            sb.Append(Loc.T("빨간 표시 — 위기인 동료(구해질 때까지)")).Append('\n');
            sb.Append(Loc.T("소리 — 냐?=의심 · 그르렁=추격 · 드르렁=깊은 잠 · 딸랑=방울 단 고양이")).Append('\n'); // 보이지 않는 고양이는 소리로 (docs/07, 고양이 311)
            // 위기 때 — 쓰러짐·끈끈이에서 돌아오는 길 (고양이 188, 181~187 흐름)
            sb.Append('\n').Append("<color=").Append(key).Append(">").Append(Loc.T("위기 때")).Append("</color>\n");
            sb.Append(Loc.T("쓰러짐 — 동료가 몸을 쥐구멍(창고)에 넣으면 삶")).Append('\n');
            sb.Append(Loc.T("끈끈이 — 동료가 옆에서 E 길게")).Append('\n');
            sb.Append(Loc.T("지치면 — 손에 든 치즈를 E 길게로 먹기 (값은 사라짐)")).Append('\n'); // 들어야만 뜨는 안내라 몰랐다 (고양이 310)
            if (Debug.isDebugBuild) sb.Append("<color=#9a9a9a>").Append(Loc.T("F3  고양이 정보 · F4  확인 메뉴 (개발용)")).Append("</color>");
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string color, string keys, string label) =>
            sb.Append("<color=").Append(color).Append('>').Append(keys).Append("</color>  ").Append(label).Append('\n');

        // 키보드·마우스 바인딩만 (패드는 따로 안내할 때) — 합성 바인딩은 머리 하나로 "W/A/S/D"
        private static string Keys(InputAction a)
        {
            var parts = new System.Collections.Generic.List<string>();
            var bindings = a.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isPartOfComposite) continue;
                string path = b.isComposite && i + 1 < bindings.Count ? bindings[i + 1].path : b.path;
                if (path.StartsWith("<Gamepad>")) continue;
                // 키 자리 이름으로 — 표시 이름은 지금 입력 언어를 따라 한글 자판이면 "ㅈ/ㅁ/ㄴ/ㅇ"가 됐다 (고양이 176)
                if (b.isComposite)
                {
                    var comp = new System.Collections.Generic.List<string>();
                    var byName = new System.Collections.Generic.Dictionary<string, string>();
                    for (int j = i + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++) { string k = KeyName(bindings[j].path); comp.Add(k); byName[bindings[j].name] = k; }
                    // 이동은 위·아래·왼·오 순서로 들어 있어 W/S/A/D가 됐다 — 익숙한 W/A/S/D로
                    if (byName.TryGetValue("up", out var u) && byName.TryGetValue("left", out var l) && byName.TryGetValue("down", out var d) && byName.TryGetValue("right", out var r))
                        parts.Add($"{u}/{l}/{d}/{r}");
                    else parts.Add(string.Join("/", comp));
                }
                else parts.Add(KeyName(b.path));
            }
            return string.Join(" / ", parts);
        }

        private static string KeyName(string path) =>
            Friendly(InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice));

        private static string Friendly(string key) => key switch
        {
            "LMB" or "Left Button" => Loc.T("좌클릭"), "RMB" or "Right Button" => Loc.T("우클릭"), "MMB" or "Middle Button" => Loc.T("휠클릭"),
            "Left Shift" => "Shift", "Left Control" or "Left Ctrl" => "Ctrl", "Escape" => "Esc",
            _ => key,
        };

#if UNITY_EDITOR
        public void EditorSetup(InputActionAsset input, GameObject tag, GameObject panel, TMP_Text list, TMP_Text title, RectTransform below) { _inputAsset = input; _tag = tag; _panel = panel; _list = list; _title = title; _below = below; }
#endif
    }
}
