using System.Collections.Generic;
using System.Text.RegularExpressions;
using RatGame.Data;
using RatGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 메뉴 화면 번역 준비 (docs/12 언어). 번역 표를 만들고, 메뉴 프리팹의 고정 한국어 글자에 LocalizedText를 붙이고,
    /// 표에 원문을 모은다. 영어는 임시(더미) 번역 — 문구 정리 작업 때 표에서 고친다. 다시 돌려도 이미 있는 건 건드리지 않는다.
    /// </summary>
    public static class LocalizationTools
    {
        private const string TablePath = "Assets/_Project/Resources/LocalizationTable.asset";

        // UI 프리팹 전부 (메뉴 3종으로 시작 — 고양이 188, HUD·패널까지 — 고양이 189)
        private const string UiPrefabFolder = "Assets/_Project/Prefabs/UI";

        private static readonly Dictionary<string, string> DummyEn = new()
        {
            // 메인 메뉴
            ["쥐도 새도 모르게"] = "Not Even a Mouse",
            ["1~4인 협동 은신 약탈"] = "1-4 player co-op stealth heist",
            ["호스트 시작"] = "Host Game",
            ["친구 방 참가"] = "Join Friend",
            ["설정"] = "Settings",
            ["종료"] = "Quit",
            ["배경 장면 자리\n쥐구멍 앞에서 쥐 4마리 대기 (아트 단계)"] = "Background scene placeholder\n4 mice waiting at the mousehole (art pass)",
            ["로컬 모드"] = "Local mode",
            ["로컬 모드 (같은 PC에서만 참가)"] = "Local mode (same PC only)",
            [" · 개발 빌드"] = " · dev build",
            ["기지를 여는 중…"] = "Opening the base…",
            ["호스트를 시작하지 못했어요. 이미 켜진 게임이 있는지 확인하세요."] = "Couldn't start hosting. Is another game already running?",
            ["친구가 보낸 초대를 수락하면 바로 들어가요.\n(Steam 친구 목록에서 \"게임 참가\"도 돼요)"] = "Accept a friend's invite to join.\n(\"Join Game\" in the Steam friends list works too)",
            ["기지에 접속하는 중…"] = "Connecting to the base…",
            ["접속을 시작하지 못했어요."] = "Couldn't start connecting.",
            ["호스트를 찾지 못했어요. 친구가 방을 열었는지 확인하세요."] = "Host not found. Did your friend open a room?",
            // 일시정지
            ["일시정지"] = "Paused",
            ["계속하기"] = "Resume",
            ["세션 나가기"] = "Leave Session",
            ["나가기"] = "Leave",
            ["취소"] = "Cancel",
            ["호스트가 나가면 친구들의 세션도 끝나요.\n메인 메뉴로 나갈까요?"] = "If the host leaves, everyone's session ends.\nReturn to main menu?",
            ["메인 메뉴로 나갈까요?\n친구들은 계속 플레이해요."] = "Return to main menu?\nYour friends keep playing.",
            ["Esc · 계속하기   (게임은 멈추지 않아요)"] = "Esc · Resume   (the game keeps running)",
            ["내가 연 방"] = "My room",
            ["친구 방"] = "Friend's room",
            ["명"] = " players",
            // 설정
            ["조작"] = "Controls",
            ["마우스 감도"] = "Mouse sensitivity",
            ["웅크리기"] = "Crouch",
            ["화면 흔들림"] = "Camera shake",
            ["화면"] = "Display",
            ["화면 모드"] = "Screen mode",
            ["해상도"] = "Resolution",
            ["언어"] = "Language",
            ["소리"] = "Audio",
            ["전체"] = "Master",
            ["효과음"] = "Effects",
            ["음악"] = "Music",
            ["보이스"] = "Voice",
            ["기본값"] = "Defaults",
            ["닫기"] = "Close",
            ["적용"] = "Apply",
            ["누르고 있기"] = "Hold",
            ["눌러서 전환"] = "Toggle",
            ["켬"] = "On",
            ["끔"] = "Off",
            ["전체 화면"] = "Fullscreen",
            ["테두리 없는 창"] = "Borderless",
            ["창 모드"] = "Windowed",
            ["기본값으로 바꿨어요. [적용]을 눌러야 저장돼요."] = "Reset to defaults. Press [Apply] to save.",
            ["적용했어요."] = "Applied.",
            ["[적용]을 누르면 언어가 바뀌어요."] = "Press [Apply] to change the language.",
            // 게임 중 HUD·패널 (고양이 189)
            ["[E] 도감"] = "[E] Codex",
            ["[E] 거울"] = "[E] Mirror",
            ["[E] 쥐 상점"] = "[E] Rat Shop",
            ["도감"] = "Codex",
            ["닫기 (E / Esc)"] = "Close (E / Esc)",
            ["금 감"] = "Cracked",
            ["<b>F1</b>  조작"] = "<b>F1</b>  Controls",
            ["벽 속 지도  <size=70%>(팀이 가 본 방 · 주황 = 목적지 · 금색 = 보물방 · 파란 선 = 배관)</size>"] = "Map of the walls  <size=70%>(rooms your team visited · orange = destination · gold = treasure room · blue line = pipe)</size>",
            ["수확"] = "Haul",
            ["들고 온 것"] = "Carried home",
            ["누계"] = "Total",
            ["오늘의 쥐들"] = "Today's rats",
            ["오늘의 일꾼"] = "Top worker",
            ["다운"] = "Down",
            ["새 도감"] = "New codex",
            ["거울 — 스킨·털 색"] = "Mirror — skins & fur color",
            ["털 색 팔레트"] = "Fur color palette",
            ["색상"] = "Hue",
            ["채도"] = "Saturation",
            ["밝기"] = "Brightness",
            ["이 색 입기"] = "Wear this color",
            ["쥐 상점"] = "Rat Shop",
            ["상점 — 다음 맵 준비"] = "Shop — prepare for the next map",
            ["사기"] = "Buy",
            // F1 조작 안내 (고양이 190)
            ["조작  <size=70%>(F1 닫기)</size>"] = "Controls  <size=70%>(F1 to close)</size>",
            ["이동"] = "Move",
            ["달리기"] = "Sprint",
            ["점프"] = "Jump",
            ["잡기 · 놓기"] = "Grab · drop",
            ["던지기 (누르고 있다 떼기)"] = "Throw (hold, then release)",
            ["상호작용 ([E] 안내가 뜨면)"] = "Interact (when an [E] prompt shows)",
            ["찍찍 (소리 남 · 쓰러져서도 됨)"] = "Squeak (makes noise · works while down)",
            ["핑"] = "Ping",
            ["킁킁 — 목적지·음식 냄새"] = "Sniff — destination & food scent",
            ["주머니 칸 고르기"] = "Pick pocket slot",
            ["지도 (벽 속, 누르고 있기)"] = "Map (in the walls, hold)",
            ["메뉴"] = "Menu",
            ["화면 읽기"] = "Reading the screen",
            ["고양이 위 <b>?</b> 의심(밑에 이유) · <b>!</b> 추격"] = "Above a cat: <b>?</b> suspicious (reason below) · <b>!</b> chasing",
            ["오른쪽 아래 — 내 소리 크기 · 냄새 남기는 중"] = "Bottom right — how loud you are · leaving scent",
            ["빨간 표시 — 위기인 동료(구해질 때까지)"] = "Red marker — teammate in trouble (until rescued)",
            ["위기 때"] = "When in trouble",
            ["쓰러짐 — 동료가 몸을 쥐구멍(창고)에 넣으면 삶"] = "Down — a teammate carries your body into the mousehole (storeroom)",
            ["끈끈이 — 동료가 옆에서 E 길게"] = "Glue trap — a teammate holds E next to you",
            ["F3  고양이 정보 · F4  확인 메뉴 (개발용)"] = "F3  cat info · F4  check menu (dev)",
            ["좌클릭"] = "LMB",
            ["우클릭"] = "RMB",
            ["휠클릭"] = "MMB",
            // 위 상태 막대 RunBar (고양이 191) — {0}은 숫자·이름 자리 (Loc.F)
            ["[Enter] 출발   누계 {0}"] = "[Enter] Depart   Total {0}",
            ["호스트 출발 대기   누계 {0}"] = "Waiting for host to depart   Total {0}",
            ["스테이지 {0}/{1}   식량 {2}/{3}"] = "Stage {0}/{1}   Food {2}/{3}",
            ["쥐구멍 {0}   누계 {1}"] = "Mousehole {0}   Total {1}",
            [" · 쓰러진 {0}명 두고 감"] = " · leaving {0} downed behind",
            ["다음으로"] = "Next",
            ["귀환 중"] = "Heading home",
            [" · 빈손"] = " · empty-handed",
            ["식량이 모자라요 — {0} 더 모아 창고에"] = "Not enough food — bring {0} more to the storeroom",
            ["창고에 모이면 다음으로   {0}/{1}"] = "Gather in the storeroom to move on   {0}/{1}",
            ["쥐구멍에 모이면 귀환   {0}/{1}"] = "Gather at the mousehole to go home   {0}/{1}",
            [" · 아직 빈손이에요"] = " · still empty-handed",
            ["할당량 채움! 창고에 모이면 다음 · 상점 돈 +{0}"] = "Quota met! Gather in the storeroom · shop money +{0}",
            ["   · 정전"] = "   · Blackout",
            ["   · 덫 대방출"] = "   · Trap sale",
            ["   · 간식 날"] = "   · Treat day",
            ["   · 집주인 외출"] = "   · Owner out",
            ["   · 분주한 집"] = "   · Busy house",
            ["   · 고양이 손님"] = "   · Cat guest",
            ["출발…"] = "Departing…",
            ["출발 발판에 모이면 출발   {0}/{1}"] = "Gather on the departure pad to go   {0}/{1}",
            ["   최고 스테이지 {0}"] = "   Best stage {0}",
            [" · 엔딩 {0}번"] = " · endings {0}",
            ["누계 {0}   목적지 {1}"] = "Total {0}   Destination {1}",
            ["창고"] = "Storeroom",
            ["부엌(생성)"] = "Kitchen (generated)",
            ["벽 속(생성)"] = "Inside the walls (generated)",
        };

        // 스크립트가 들고 있지만 글자는 안 쓰는 것 (색·위치만 읽음) — 번역해도 된다
        private static readonly HashSet<string> StaticRefs = new() { "ControlsHelpWidget._title" };

        // 코드가 복제해 글자를 새로 쓰는 틀(핑 이름·알림·결과 칩) 또는 Find로 찾아 덮어쓰는 글자 — 붙이면 켜질 때 원문으로 되돌린다
        private static readonly HashSet<string> CodeTemplates = new() { "회색 쥐", "도감 등록!  계란", "계란", "쥐구멍 적립" };

        private static readonly Regex Hangul = new("[가-힣]");

        [MenuItem("Tools/RatGame/UI/Localize Menus")]
        public static void LocalizeMenus()
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTableSO>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LocalizationTableSO>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            int added = 0, attached = 0;
            foreach (var kv in DummyEn)
                if (table.EditorAdd(kv.Key, kv.Value)) added++;

            var missingEn = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var codeDriven = CollectCodeDriven(root);
                bool changed = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!Hangul.IsMatch(text.text) || codeDriven.Contains(text)) continue;
                    if (CodeTemplates.Contains(text.text))
                    {
                        var wrong = text.GetComponent<LocalizedText>();
                        if (wrong != null) { Object.DestroyImmediate(wrong, true); changed = true; }
                        if (table.EditorRemove(text.text)) added--;
                        continue;
                    }
                    // 중첩 프리팹(설정 패널 안의 행 등)은 그 프리팹 쪽에서 붙인다
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;
                    if (table.EditorAdd(text.text, DummyEn.TryGetValue(text.text, out var en) ? en : "")) added++;
                    if (string.IsNullOrEmpty(en)) missingEn.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}/{text.name}: {text.text.Replace("\n", "\\n")}");
                    var loc = text.GetComponent<LocalizedText>();
                    if (loc != null && loc.Ko == text.text) continue;
                    if (loc == null) { loc = text.gameObject.AddComponent<LocalizedText>(); attached++; }
                    loc.Ko = text.text;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            int missing = 0, badSlots = 0;
            foreach (var e in table.Entries)
            {
                if (string.IsNullOrEmpty(e.En)) { missing++; continue; }
                // Loc.F 틀 — 영어에 {n} 자리가 하나라도 다르면 string.Format이 게임 중에 터진다
                if (Slots(e.Ko) != Slots(e.En)) { badSlots++; Debug.LogError($"[Rat] 번역 자리 불일치: \"{e.Ko}\" → \"{e.En}\""); }
            }
            foreach (var m in missingEn) Debug.Log($"[Rat] 번역 없음 — {m}");
            Debug.Log($"[Rat] 메뉴 번역 준비: 표 {table.Entries.Count}줄(새 {added}, 영어 빈 칸 {missing}, 자리 불일치 {badSlots}) · LocalizedText 새로 {attached}개");
        }

        private static readonly Regex Slot = new(@"\{\d+[^}]*\}");

        private static string Slots(string s)
        {
            var list = new List<string>();
            foreach (Match m in Slot.Matches(s)) list.Add(m.Value);
            list.Sort(System.StringComparer.Ordinal);
            return string.Join(",", list);
        }

        // 스크립트가 [SerializeField]로 들고 있는 글자 = 코드가 쓰는 글자 (LocalizedText를 붙이면 원문이 엉뚱하게 잡힌다)
        private static HashSet<TMP_Text> CollectCodeDriven(GameObject root)
        {
            var set = new HashSet<TMP_Text>();
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is TMP_Text) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (StaticRefs.Contains($"{mb.GetType().Name}.{it.name}")) continue;
                    if (it.objectReferenceValue is TMP_Text t && set.Add(t) && Hangul.IsMatch(t.text))
                        Debug.Log($"[Rat] 번역 도구: 코드가 쓰는 글자로 보고 뺌 — {mb.GetType().Name}.{it.name} \"{t.text}\"");
                }
            }
            return set;
        }
    }
}
