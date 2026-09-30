using System.Collections.Generic;
using System.IO;
using System.Text;
using RatGame.Core;
using RatGame.Run;
using RatGame.UI;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SettingsService = RatGame.Core.SettingsService;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 통째 시험 하네스 (고양이 199). 기지 → 벽 속 1~5 → 엔딩 → 기지를 혼자(호스트) 자동으로 돈다.
    /// 예전 하네스(소크 3~13)는 세션 임시 폴더에 있다 사라져서 프로젝트에 둔다. 세이브는 시작 때 떠 두고 끝나면 되돌린다.
    /// 결과는 콘솔 "[Rat] 통째 시험" 줄 — 경고·오류는 첫 줄 기준 종류별.
    /// </summary>
    [InitializeOnLoad]
    public static partial class SoakHarness
    {
        private const string ArmedKey = "RatGame.Soak.Armed";
        private const float StageTimeout = 90f;
        private const int QuotaMargin = 20;

        private enum Step { Menu, Hub, WaitStage, Stage, Result, Done }

        private static Step _step;
        private static float _stepAt, _startAt, _stageStartAt;
        private static int _stagesSeen, _itemsMoved;
        private static bool _sawFinished;
        private static readonly Dictionary<string, int> Issues = new();
        private static readonly List<string> StageLines = new();
        private static string _saveBackup, _bakBackup, _languageBefore;
        private static bool _english;
        private static float _scanAt;
        private static readonly Dictionary<string, string> KoreanSeen = new(); // 영어 모드에서 화면에 남은 한국어 (글자 → 오브젝트)
        private static readonly System.Text.RegularExpressions.Regex Hangul = new("[가-힣]");
        private const string EnglishKey = "RatGame.Soak.English";
        private static string BakPath => Path.Combine(Application.persistentDataPath, "save.bak");

        static SoakHarness()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ArmedKey, false)) Begin();
            };
        }

        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls)")]
        private static void Arm() => Arm(false);

        // 영어로 한 판 — 번역 틀 오류(예외)와 화면에 남은 한국어를 잡는다 (고양이 200)
        [MenuItem("Tools/RatGame/Test/Full Run Soak (Walls, English)")]
        private static void ArmEnglish() => Arm(true);

        private static void Arm(bool english)
        {
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetBool(EnglishKey, english);
            if (EditorApplication.isPlaying) Begin();
            else EditorApplication.isPlaying = true;
        }

        private static void Begin()
        {
            SessionState.SetBool(ArmedKey, false);
            DevPort.Choose(); // 7777이 새어 막혔으면 7778 (고양이 262)
            _step = Step.Menu; _startAt = _stepAt = Time.realtimeSinceStartup;
            _stagesSeen = 0; _itemsMoved = 0; _sawFinished = false;
            Issues.Clear(); StageLines.Clear(); KoreanSeen.Clear();
            _english = SessionState.GetBool(EnglishKey, false);
            BeginClient();
            _languageBefore = SettingsService.Current.Language;
            if (_english) SetLanguage("en");
            _saveBackup = File.Exists(SaveService.FilePath) ? File.ReadAllText(SaveService.FilePath) : null;
            _bakBackup = File.Exists(BakPath) ? File.ReadAllText(BakPath) : null;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log($"[Rat] 통째 시험 시작 (벽 속, {(_twoPlayer ? $"빌드 클라 {_clientCount}개 = {_clientCount + 1}인" : "혼자")}{(_english ? ", 영어" : "")})");
        }

        private static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log || msg.Contains("MCP-FOR-UNITY")) return;
            string key = $"{type}: {msg.Split('\n')[0]}";
            if (key.Length > 140) key = key.Substring(0, 140);
            Issues[key] = Issues.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        private static void Go(Step next) { _step = next; _stepAt = Time.realtimeSinceStartup; }
        private static float InStep => Time.realtimeSinceStartup - _stepAt;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish("플레이 모드가 끝남"); return; }
            if (Time.realtimeSinceStartup - _startAt > 600f) { Finish("10분 초과"); return; }
            string scene = SceneManager.GetActiveScene().name;
            if (_english && Time.realtimeSinceStartup >= _scanAt) { _scanAt = Time.realtimeSinceStartup + 0.5f; ScanKorean(); }
            switch (_step)
            {
                case Step.Menu:
                    var menu = Object.FindFirstObjectByType<MainMenuController>();
                    if (menu == null) { if (scene == "Hub") Go(Step.Hub); return; }
                    if (InStep < 1f) return;
                    var host = typeof(MainMenuController).GetField("_hostButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ((UnityEngine.UI.Button)host.GetValue(menu)).onClick.Invoke();
                    Go(Step.Hub);
                    return;

                case Step.Hub:
                    if (scene != "Hub" || InStep < 2f) return;
                    var pad = Object.FindFirstObjectByType<DeparturePad>();
                    if (pad == null || !pad.IsSpawned) return;
                    if (!ClientReady()) return;
                    for (int i = 0; i < 4 && !pad.DestinationName.Contains("벽 속"); i++) pad.ServerCycleDestination();
                    MoveLocalPlayer(pad.transform.position + Vector3.up * 0.5f);
                    MoveClients(pad.transform.position + Vector3.up * 0.5f);
                    if (pad.Counting.Value || InStep > 45f) Go(Step.WaitStage); // 클라 접속 대기를 포함해 넉넉히
                    return;

                case Step.WaitStage:
                    var run = RunManager.Instance;
                    if (run == null || run.Phase.Value != RunPhase.StageActive || run.GetComponent<StageQuota>() == null) { if (InStep > 40f) Finish($"스테이지가 안 열림 ({scene})"); return; }
                    if (Object.FindFirstObjectByType<DepositZone>() == null || InStep < 2f) return; // 맵 생성·스폰 여유
                    _stagesSeen++; _stageStartAt = Time.realtimeSinceStartup;
                    ClientActions(); // 클라 킁킁·핑·찍찍 (고양이 224)
                    Go(Step.Stage);
                    return;

                case Step.Stage:
                    StageTick();
                    return;

                case Step.Result:
                    if (scene == "Hub" && _sawFinished && InStep > 3f) { Finish(null); return; }
                    var r = RunManager.Instance;
                    if (r != null && r.Phase.Value == RunPhase.StageActive && !r.IsShowingResult && scene != "Hub") Go(Step.WaitStage);
                    if (InStep > 40f) Finish($"결과 뒤 멈춤 ({scene})");
                    return;
            }
        }

        private static void StageTick()
        {
            var run = RunManager.Instance;
            if (run == null) return;
            var quota = run.GetComponent<StageQuota>();
            var zone = Object.FindFirstObjectByType<DepositZone>();
            if (Time.realtimeSinceStartup - _stageStartAt > StageTimeout) { Finish($"스테이지 {quota.StageNumber.Value} 시간 초과 (적립 {run.StashedValue.Value}/{quota.Quota.Value})"); return; }
            if (run.IsShowingResult)
            {
                if (quota.Finished.Value) _sawFinished = true;
                StageLines.Add($"S{quota.StageNumber.Value} 할당량 {quota.Quota.Value} 적립 {run.StashedValue.Value} {Time.realtimeSinceStartup - _stageStartAt:0.0}초 {(run.Phase.Value == RunPhase.Wiped ? "전멸" : "통과")}");
                Go(Step.Result);
                return;
            }
            if (InStep < 0.5f) return; // 0.5초마다 한 번
            _stepAt = Time.realtimeSinceStartup;
            Vector3 into = zone.Area.bounds.center + Vector3.up * 0.3f;
            if (run.StashedValue.Value < quota.Quota.Value + QuotaMargin)
            {
                // 값 큰 순으로 하나씩 창고 칸 안에 떨군다 (호스트 권한 — 물리 물체는 호스트가 옮긴다)
                CarryableItem best = null;
                foreach (var item in Object.FindObjectsByType<CarryableItem>(FindObjectsSortMode.None))
                {
                    if (!item.IsSpawned || item.Data == null || item.Pocketed.Value || item.CarrierIds.Count > 0 || item.EffectiveValue <= 0) continue;
                    if (item.GetComponent<DownedBody>() != null || zone.Area.bounds.Contains(item.transform.position)) continue;
                    if (best == null || item.EffectiveValue > best.EffectiveValue) best = item;
                }
                if (best != null)
                {
                    var body = best.GetComponent<Rigidbody>();
                    if (body != null) { body.position = into; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
                    best.transform.position = into;
                    _itemsMoved++;
                }
                return;
            }
            MoveLocalPlayer(into);
            MoveClients(into);
        }

        private static void SetLanguage(string code)
        {
            var d = SettingsService.Current.Clone();
            d.Language = code;
            SettingsService.Apply(d);
        }

        // 켜진 글자 중 한국어 — 개발용 화면(F3 고양이 정보·F4 확인 메뉴)은 뺀다
        private static void ScanKorean()
        {
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || !Hangul.IsMatch(t.text)) continue;
                if (t.GetComponentInParent<CatDebugOverlay>() != null || t.GetComponentInParent<DevCheckMenu>() != null) continue;
                if (Invisible(t)) continue; // 알파 0 그룹 안(숨는 중 화면처럼 안 보일 때) — 보이는 글자만 센다
                string key = t.text.Replace("\n", " / ");
                if (key.Length > 80) key = key.Substring(0, 80);
                if (!KoreanSeen.ContainsKey(key)) KoreanSeen[key] = $"{SceneManager.GetActiveScene().name}/{t.transform.parent?.name}/{t.name}";
            }
        }

        private static bool Invisible(Component c)
        {
            foreach (var g in c.GetComponentsInParent<CanvasGroup>())
                if (g.alpha < 0.01f) return true;
            return false;
        }

        private static void MoveLocalPlayer(Vector3 pos)
        {
            var nm = NetworkManager.Singleton;
            var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (player == null) return;
            var body = player.GetComponent<Rigidbody>();
            if (body != null) { body.position = pos; if (!body.isKinematic) body.linearVelocity = Vector3.zero; } // 잡힘·숨음이면 키네마틱 — 속도를 넣으면 오류
            player.transform.position = pos;
        }

        private static void Finish(string failure)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseInt(RatGame.Net.NetworkLauncher.DevPortKey); // 시험 밖 플레이는 늘 기본 포트
            var sb = new StringBuilder();
            sb.Append(failure == null ? "[Rat] 통째 시험 끝 — 성공" : $"[Rat] 통째 시험 끝 — 실패: {failure}");
            sb.Append($" | {Time.realtimeSinceStartup - _startAt:0}초 · 스테이지 {_stagesSeen} · 옮긴 물건 {_itemsMoved} · 엔딩 {(_sawFinished ? "봄" : "못 봄")}");
            foreach (var line in StageLines) sb.Append(" | ").Append(line);
            if (_twoPlayer) sb.Append($" | 클라 행동 {_clientActions}번 → 호스트가 받은 핑 {_pingsSeen} · 찍찍 자막 {_squeaksSeen}");
            if (_english)
            {
                sb.Append($" | 영어 모드 화면 한국어 {KoreanSeen.Count}종");
                foreach (var kv in KoreanSeen) sb.Append($" | [{kv.Value}] {kv.Key}");
                SetLanguage(_languageBefore);
            }
            sb.Append($" | 경고·오류 {Issues.Count}종");
            foreach (var kv in Issues) sb.Append($" | {kv.Value}× {kv.Key}");
            // 파일만 되돌린다 — 게임 안 메모리는 그대로라 이 플레이를 계속하면 다시 쓸 수 있다: 끝나면 플레이를 멈출 것
            sb.Append(CloseClientAndReport());
            if (_saveBackup != null) { File.WriteAllText(SaveService.FilePath, _saveBackup); sb.Append(" | 세이브 되돌림"); }
            if (_bakBackup != null) File.WriteAllText(BakPath, _bakBackup);
            Debug.Log(sb.ToString());
        }
    }
}
