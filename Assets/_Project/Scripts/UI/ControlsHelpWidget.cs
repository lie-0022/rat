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
            ("Grab", "잡기 · 놓기"), ("Throw", "던지기 (누르고 있다 떼기)"), ("Interact", "상호작용"),
            ("Squeak", "찍찍 (소리 남)"), ("Ping", "핑"), ("Sniff", "킁킁 — 목적지 쪽 냄새"),
        };

        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private GameObject _tag;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _list;
        [SerializeField] private TMP_Text _title; // 키 글자색 = 제목 강조색 (테마를 따라가게)
        [SerializeField] private RectTransform _below; // 팀 상자 — 인원 수만큼 길어져서 그 아래를 따라간다 (고양이 83)
        private const float Gap = 10f;

        private bool _open;

        private void Start() => Show(false);

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
                Line(sb, key, Keys(a), label);
            }
            Line(sb, key, "1~4", "주머니 칸 고르기");
            Line(sb, key, "Tab", "지도 (벽 속, 누르고 있기)");
            Line(sb, key, "Esc", "메뉴");
            if (Debug.isDebugBuild) sb.Append("<color=#9a9a9a>F3  고양이 정보 · F4  확인 메뉴 (개발용)</color>");
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
                parts.Add(Friendly(a.GetBindingDisplayString(i)));
            }
            return string.Join(" / ", parts);
        }

        private static string Friendly(string key) => key switch
        {
            "LMB" => "좌클릭", "RMB" => "우클릭", "MMB" => "휠클릭",
            "Left Shift" => "Shift", "Left Control" => "Ctrl",
            _ => key,
        };

#if UNITY_EDITOR
        public void EditorSetup(InputActionAsset input, GameObject tag, GameObject panel, TMP_Text list, TMP_Text title, RectTransform below) { _inputAsset = input; _tag = tag; _panel = panel; _list = list; _title = title; _below = below; }
#endif
    }
}
