using System;
using System.Collections.Generic;
using System.Text;
using RatGame.AI;
using RatGame.Player;
using RatGame.UI;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 짧은 플레이 시험 (고양이 206) — 리팩터 뒤 버튼 하나로 다시 돌리려고. 혼자(호스트), 창고 맵.
    ///  - Carry Input Test: 실제 입력(마우스·숫자 키)을 넣어 집기 → 칸 바꾸기(주머니) → 던지기 → 내려놓기 → 대형 끌기·놓기.
    ///  - Cat Chase Test: 쥐를 고양이 앞 2m에 붙여 순찰 → 추격 → 포획 → 복귀까지.
    /// 결과 한 줄 "[Rat] 플레이 시험 … 통과/실패". 세이브는 런을 끝내지 않아 안 바뀐다.
    /// </summary>
    [InitializeOnLoad]
    public static partial class PlayTests
    {
        private const string ArmedKey = "RatGame.PlayTest.Armed";

        private class Step
        {
            public string Name;
            public float Wait;              // 이전 단계에서 이만큼 지난 뒤
            public Func<bool> Ready;        // 추가 대기 조건 (null이면 없음)
            public Action Act;
            public Func<string> Check;      // null이면 통과, 아니면 실패 사유
        }

        private static List<Step> _steps;
        private static int _index;
        private static double _stepAt, _startAt;
        private static string _name;
        private static readonly StringBuilder Report = new();
        private static int _fails;
        private static string _skip; // 시험 도구가 확인할 수 없는 상태(예: 창 초점 없음) — 실패 대신 건너뜀 (고양이 239)

        static PlayTests()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                // 플레이를 멈출 때 게임이 한 번 더 저장해 시험 끝의 되돌리기를 덮는다 — 멈춘 뒤 한 번 더 (고양이 234)
                if (s == PlayModeStateChange.EnteredEditMode && RestoreAfterPlay != null)
                {
                    RestoreAfterPlay();
                    RestoreAfterPlay = null;
                    Debug.Log("[Rat] 플레이 시험: 플레이 멈춘 뒤 세이브 한 번 더 되돌림");
                }
                if (s == PlayModeStateChange.EnteredEditMode) ContinueQueue(); // 여러 시험 차례로 (고양이 238)
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                string armed = SessionState.GetString(ArmedKey, "");
                SessionState.SetString(ArmedKey, "");
                if (armed == "carry") Begin("운반 입력", CarrySteps());
                else if (armed == "chase") Begin("고양이 추격", ChaseSteps());
                else if (armed == "heavy2p") Begin("대형 2인 운반", HeavyCarrySteps(), false);
                else if (armed == "xl4p") Begin("특대 4인 운반", ExtraLargeSteps(), false);
                else if (armed == "rescue2p") Begin("쓰러진 동료 구조", RescueSteps(), false);
                else if (armed == "achv") Begin("도전과제 판정", AchievementSteps());
                else if (armed == "glue2p") Begin("끈끈이 구출 E 홀드", GlueSteps(), false);
                else if (armed == "codexall") Begin("전리품 전부 정산", CodexAllSteps());
                else if (armed == "sync4p") Begin("4인 던지기 동기화", SyncSteps(), false);
                else if (armed == "chase2p") Begin("고양이 추격 2인", Chase2PSteps(), false);
                else if (armed == "attention") Begin("관심 점수", AttentionSteps());
                else if (armed == "modifiers") Begin("오늘의 집 7종", ModifierSteps(), false);
                else if (armed == "rehost") Begin("나갔다 다시 호스트", RehostSteps(), false);
                else if (armed == "laser2p") Begin("클라 레이저 2인", LaserSteps(), false);
                else if (armed == "leave2p") Begin("클라 이탈 2인", LeaveSteps(), false);
                else if (armed == "hostleave2p") Begin("호스트 이탈 2인", HostLeaveSteps(), false);
            };
        }

        [MenuItem("Tools/RatGame/Test/Carry Input Test")]
        private static void ArmCarry() => Arm("carry");

        [MenuItem("Tools/RatGame/Test/Cat Chase Test")]
        private static void ArmChase() => Arm("chase");

        private static void Arm(string which)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Rat] 플레이 시험은 플레이 모드가 꺼진 상태에서 시작 (메인 메뉴부터)"); return; }
            SessionState.SetString(ArmedKey, which);
            EditorApplication.isPlaying = true;
        }

        private static void Begin(string name, List<Step> body, bool warehouse = true)
        {
            _name = name;
            _steps = warehouse ? new List<Step>(ToWarehouse()) : new List<Step>();
            _steps.AddRange(body);
            _index = 0; _fails = 0; _skip = null; Report.Clear();
            DevPort.Choose(); // 7777이 새어 막혔으면 7778 (고양이 262)
            BackupSave(name); // 모든 시험이 세이브를 떠 두고 플레이를 멈춘 뒤 되돌린다 — 새 시험이 빠뜨려도 (고양이 297, 296에서 한 번 빠뜨림)
            _stepAt = _startAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup - _startAt > 180) { Fail("3분 초과"); Done(); return; } // 2인 시험은 빌드 클라 켜는 데 수십 초
            if (_index >= _steps.Count && _name == "고양이 추격") Report.Append($" | 전이 {string.Join(",", Transitions)}");
            if (_index >= _steps.Count) { Done(); return; }
            var s = _steps[_index];
            if (EditorApplication.timeSinceStartup - _stepAt < s.Wait) return;
            if (s.Ready != null && !s.Ready()) return;
            try
            {
                s.Act?.Invoke();
                string bad = s.Check?.Invoke();
                if (bad != null) Fail($"{s.Name}: {bad}");
                else if (s.Check != null) Report.Append($" | {s.Name} ✓");
            }
            catch (Exception e) { Fail($"{s.Name}: {e.GetType().Name} {e.Message}"); }
            _index++;
            _stepAt = EditorApplication.timeSinceStartup;
        }

        private static Action OnDone; // 시험별 정리 (빌드 클라 닫기 등)

        private static void Fail(string why) { _fails++; Report.Append($" | ✗ {why}"); }

        private static void Done()
        {
            EditorApplication.update -= Tick;
            OnDone?.Invoke();
            SessionState.EraseInt(RatGame.Net.NetworkLauncher.DevPortKey); // 시험 밖 플레이는 늘 기본 포트
            OnDone = null;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            string verdict = _fails > 0 ? $"실패 {_fails}" : _skip != null ? $"건너뜀 ({_skip})" : "통과";
            Debug.Log($"[Rat] 플레이 시험 {_name} — {verdict} ({EditorApplication.timeSinceStartup - _startAt:0}초){Report}");
            QueueResult(_name, _fails > 0 ? "✗" : _skip != null ? "–" : "✓");
        }

        // ---- 공통: 메인 메뉴 → 호스트 → 기지 발판 → 창고 ----

        private static IEnumerable<Step> ToWarehouse()
        {
            yield return new Step
            {
                Name = "호스트", Wait = 1f, Ready = () => UnityEngine.Object.FindFirstObjectByType<MainMenuController>() != null,
                Act = () =>
                {
                    var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                    var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
                }
            };
            // 발판에 계속 세워 둔다 — 스폰 직후 한 번만 옮기면 스폰 자리로 덮인다
            yield return new Step
            {
                Name = "창고 도착", Wait = 1f,
                Ready = () =>
                {
                    if (SceneManager.GetActiveScene().name == "Stage_Warehouse01") return Me() != null;
                    var pad = UnityEngine.Object.FindFirstObjectByType<DeparturePad>();
                    if (pad != null && Me() != null) Put(pad.transform.position + Vector3.up * 0.5f);
                    return false;
                }
            };
            yield return new Step { Name = "맵 안정", Wait = 3f };
        }

        // ---- 운반 입력 ----

        private static List<Step> CarrySteps() => new()
        {
            new Step { Name = "치즈 집기", Act = () => { Front(Item("loot_cheese"), 0.45f); Press(MouseButton.Left); } },
            new Step { Name = "치즈 집기", Wait = 0.4f, Act = () => { Release(); PressKey(Key.Digit2); },
                Check = () => Carry().CarriedItem == Item("loot_cheese") ? null : $"손에 {Carry().CarriedItem?.name ?? "없음"}" },
            new Step { Name = "2번 칸 → 치즈 주머니", Wait = 0.4f, Act = () => { PressKey(null); Front(Item("LaserPointer"), 0.45f); Press(MouseButton.Left); },
                Check = () => Item("loot_cheese").Pocketed.Value ? null : "치즈가 주머니에 없음" },
            new Step { Name = "레이저 집기", Wait = 0.4f, Act = () => { Release(); Press(MouseButton.Right); },
                Check = () => Carry().CarriedItem == Item("LaserPointer") ? null : $"손에 {Carry().CarriedItem?.name ?? "없음"}" },
            new Step { Name = "던지기 차지", Wait = 1.2f, Act = Release,
                Check = () => Carry().ThrowCharge > 0.9f || !Carry().IsHolding ? null : $"차지 {Carry().ThrowCharge:0.00}" },
            new Step { Name = "던짐", Wait = 0.15f, Act = () => PressKey(Key.Digit1),
                Check = () => !Carry().IsHolding && Item("LaserPointer").GetComponent<Rigidbody>().linearVelocity.magnitude > 3f ? null
                    : $"손={Carry().IsHolding} 속도={Item("LaserPointer").GetComponent<Rigidbody>().linearVelocity.magnitude:0.0}" },
            new Step { Name = "1번 칸 → 치즈", Wait = 0.4f, Act = () => { PressKey(null); Press(MouseButton.Left); },
                Check = () => Carry().CarriedItem == Item("loot_cheese") ? null : $"손에 {Carry().CarriedItem?.name ?? "없음"}" },
            new Step { Name = "내려놓기", Wait = 0.4f, Act = () => { Release(); Front(Item("loot_phone"), 0.45f); },
                Check = () => !Carry().IsHolding && !Item("loot_cheese").Pocketed.Value ? null : "아직 들고 있음" },
            new Step { Name = "대형 잡기", Wait = 0.4f, Act = () => Press(MouseButton.Left) },
            new Step { Name = "대형 끌기", Wait = 0.4f, Act = Release,
                Check = () => Carry().IsDraggingHeavy ? null : "안 잡힘" },
            new Step { Name = "대형 놓기", Wait = 0.6f, Act = () => Press(MouseButton.Left) },
            new Step { Name = "대형 놓기", Wait = 0.4f, Act = Release, Check = () => !Carry().IsHolding ? null : "아직 끌고 있음" },
        };

        // ---- 고양이 추격 ----

        private static readonly List<string> Transitions = new();
        private static readonly Action<CatBrain, CatState, CatState> OnCat = (c, a, b) => Transitions.Add($"{a}→{b}");

        private static List<Step> ChaseSteps() => new()
        {
            new Step { Name = "고양이 깨우기", Act = () =>
            {
                Transitions.Clear();
                CatBrain.ServerStateChanged -= OnCat;
                CatBrain.ServerStateChanged += OnCat;
                Cat().ServerWake();
                Cat().ServerAttachBell(); // 방울은 행동을 안 바꾼다 — 같은 판에서 움직일 때 딸랑도 확인 (고양이 253)
            } },
            // 순찰·의심·복귀·잠이면 앞 2m에 계속 붙인다 (2m 안 목격 = 바로 추격, docs/07). 걷는 고양이라 한 번 두면 금방 멀어진다
            new Step { Name = "추격", Ready = () =>
            {
                var st = Cat().State.Value;
                if (Transitions.Contains("Chase→Capture")) return true;
                // 쫓는 중이 아니면 늘 다시 앞에 — 전엔 순찰·의심·복귀·잠만이라 매복·상자 앉기면 3분을 다 썼다 (고양이 272, 추격 2인은 250에서)
                if (st is not (CatState.Chase or CatState.Capture or CatState.Toy))
                {
                    if (st == CatState.Ambush || st == CatState.BoxSit) Cat().ServerWake(); // 숨어 있으면 앞에 서도 못 본다
                    var fwd = Cat().transform.forward; fwd.y = 0f; fwd.Normalize();
                    Put(Cat().transform.position + fwd * 2f + Vector3.up * 0.3f);
                }
                return false;
            }, Check = () => Transitions.Exists(t => t.EndsWith("→Chase")) ? null : string.Join(",", Transitions) },
            new Step { Name = "포획 뒤 복귀", Ready = () => Transitions.Contains("Capture→Return") || EditorApplication.timeSinceStartup - _stepAt > 15,
                Check = () => Transitions.Contains("Capture→Return") ? null : string.Join(",", Transitions) },
            // 복귀를 벗어나면 통과 — 근처에 쥐가 남아 있으면 순찰 대신 다시 의심으로 가는 게 맞다 (첫 시험에서 Return→Suspicious)
            new Step { Name = "복귀 뒤", Ready = () => Transitions.Exists(t => t.StartsWith("Return→")) || EditorApplication.timeSinceStartup - _stepAt > 15,
                Act = () => CatBrain.ServerStateChanged -= OnCat,
                Check = () => Transitions.Exists(t => t.StartsWith("Return→")) ? null : string.Join(",", Transitions) },
            new Step { Name = "방울 딸랑", Check = () =>
            {
                var voice = Cat().GetComponent<RatGame.AI.CatVoice>();
                int n = voice != null ? (int)typeof(RatGame.AI.CatVoice).GetField("_jingles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(voice) : 0;
                Report.Append($" | 방울 딸랑 {n}번");
                return n > 0 ? null : "방울 단 고양이가 움직였는데 소리 없음";
            } },
        };

        // ---- 도우미 ----

        private static NetworkObject Me()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        }

        private static PlayerCarryController Carry() => Me().GetComponent<PlayerCarryController>();
        private static CatBrain Cat() => UnityEngine.Object.FindFirstObjectByType<CatBrain>();

        private static CarryableItem Item(string prefix)
        {
            foreach (var it in UnityEngine.Object.FindObjectsByType<CarryableItem>(FindObjectsSortMode.None))
                if (it.name.StartsWith(prefix)) return it;
            throw new Exception($"{prefix} 없음");
        }

        private static void Put(Vector3 pos)
        {
            var body = Me().GetComponent<Rigidbody>();
            if (body != null) { body.position = pos; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
            Me().transform.position = pos;
            Me().transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        // 물건 콜라이더 앞(−z) 가장자리에서 gap만큼 — 큰 물건은 중심 기준이면 잡기 거리 밖이다 (고양이 204)
        private static void Front(CarryableItem item, float gap)
        {
            var b = item.GetComponentInChildren<Collider>().bounds;
            Put(new Vector3(b.center.x, Me().transform.position.y, b.min.z - gap));
        }

        private static void Press(MouseButton button) => InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(button, true));
        private static void Release() => InputSystem.QueueStateEvent(Mouse.current, new MouseState());
        private static void PressKey(Key? key) => InputSystem.QueueStateEvent(Keyboard.current, key.HasValue ? new KeyboardState(key.Value) : new KeyboardState());
    }
}
