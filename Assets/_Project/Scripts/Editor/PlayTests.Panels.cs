using System.Collections.Generic;
using System.IO;
using RatGame.Core;
using RatGame.Data;
using RatGame.Run;
using RatGame.UI;
using RatGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using SettingsService = RatGame.Core.SettingsService;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 기지 창 글자 (고양이 315) — 통째 시험은 창을 안 열어 상점·거울·도감·설정·일시정지는 넘침·번역 검사를 못 받았다.
    /// 한국어·영어로 창을 하나씩 열어 글자 넘침(TMP isTextOverflowing)과 영어 화면의 한글을 센다. 설정 파일은 바이트 그대로 되돌린다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Hub Panels Text (ko+en)")]
        private static void ArmPanels() => Arm("panels");

        private static readonly string[] MenuFailures =
        {
            "네트워크 설정을 찾지 못했어요.", "Steam 방을 만들지 못했어요. Steam 연결을 확인하세요.", "호스트 시작 실패",
            "친구가 보낸 Steam 초대를 수락하면 들어갈 수 있어요.", "친구 방에 들어가지 못했어요. 방이 닫혔거나 가득 찼을 수 있어요.",
            "내가 연 방에는 참가할 수 없어요.", "클라이언트 접속 실패", "정원 초과 (최대 4명)", "게임 진행 중에는 참가할 수 없음 (로비에서만 합류)",
            "다음 맵으로 이동 중 — 기지로 돌아오면 참가할 수 있어요", "호스트를 찾지 못했어요. 친구가 방을 열었는지 확인하세요.",
            "호스트와 연결이 끊겨 메인 메뉴로 돌아왔어요.", "호스트를 시작하지 못했어요. 이미 켜진 게임이 있는지 확인하세요.",
        };

        private static readonly string[] PanelNames = { "상점", "거울", "도감", "설정", "일시정지" };

        private static void OpenPanel(int i)
        {
            switch (i)
            {
                case 0: EventBus.RaiseWorldPanelRequested(WorldPanelKind.Shop, Object.FindFirstObjectByType<VendingMachine>()); break;
                case 1: EventBus.RaiseWorldPanelRequested(WorldPanelKind.Mirror, Object.FindFirstObjectByType<Mirror>()); break;
                case 2: EventBus.RaiseWorldPanelRequested(WorldPanelKind.Codex, Object.FindFirstObjectByType<CodexBook>()); break;
                case 3: Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include).Open(); break;
                case 4:
                {
                    var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
                    pause.Open();
                    // 초대 버튼은 Steam 오버레이가 있을 때만 — 시험에선 억지로 켜서 글자도 검사 (고양이 325)
                    var invite = (UnityEngine.UI.Button)typeof(PauseMenu).GetField("_inviteButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pause);
                    if (invite != null) invite.gameObject.SetActive(true);
                    break;
                }
            }
        }

        private static void ClosePanels()
        {
            foreach (var p in Object.FindObjectsByType<ShopPanel>(FindObjectsSortMode.None)) if (p.IsOpen) p.Close();
            foreach (var p in Object.FindObjectsByType<MirrorPanel>(FindObjectsSortMode.None)) if (p.IsOpen) p.Close();
            foreach (var p in Object.FindObjectsByType<CodexPanel>(FindObjectsSortMode.None)) if (p.IsOpen) p.Close();
            foreach (var p in Object.FindObjectsByType<SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p.IsOpen) p.Close();
            foreach (var p in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p.IsOpen) p.Close(false);
        }

        private static List<Step> PanelSteps()
        {
            string settingsPath = Path.Combine(Application.persistentDataPath, "settings.json");
            byte[] settingsBytes = null;
            var steps = new List<Step>
            {
                new Step { Name = "설정 뜨기", Act = () =>
                {
                    _koreanLeft.Clear(); _overflow.Clear();
                    settingsBytes = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
                    System.Action restore = () => { if (settingsBytes != null) File.WriteAllBytes(settingsPath, settingsBytes); };
                    var prev = RestoreAfterPlay; RestoreAfterPlay = () => { prev?.Invoke(); restore(); };
                    OnDone = () => { SetLang("ko"); restore(); };
                    SetLang("ko");
                } },
                // 메뉴 상태 줄 — 접속 실패·거절 사유를 영어로 하나씩 띄워 넘침·한글 (고양이 318, 전엔 실패 문구가 번역 없이 떴다)
                new Step { Name = "메뉴 실패 문구", Wait = 1f, Ready = () => Object.FindFirstObjectByType<MainMenuController>() != null, Act = () =>
                {
                    SetLang("en");
                    var menu = Object.FindFirstObjectByType<MainMenuController>();
                    var set = typeof(MainMenuController).GetMethod("SetStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var status = (TMPro.TMP_Text)typeof(MainMenuController).GetField("_statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu);
                    foreach (var ko in MenuFailures)
                    {
                        set.Invoke(menu, new object[] { Loc.T(ko), UiColorRole.DangerText });
                        ScanOverflow("메뉴 상태", status);
                        ScanHangul("메뉴 상태", status);
                    }
                    set.Invoke(menu, new object[] { "", UiColorRole.TextMuted });
                    SetLang("ko");
                } },
                HostFromMenu("호스트"), InHub("기지"),
            };
            foreach (var lang in new[] { "ko", "en" })
            {
                var code = lang;
                steps.Add(new Step { Name = $"{code}로", Wait = 0.5f, Act = () => SetLang(code) });
                for (int i = 0; i < PanelNames.Length; i++)
                {
                    int idx = i;
                    steps.Add(new Step { Name = $"{PanelNames[idx]} 열기 ({code})", Wait = 0.5f, Act = () => OpenPanel(idx) });
                    steps.Add(new Step { Name = $"{PanelNames[idx]} 글자 ({code})", Wait = 0.8f, Act = () =>
                    {
                        string where = $"{PanelNames[idx]}·{code}";
                        ScanOverflow(where);
                        if (code == "en") ScanHangul(where);
                        ClosePanels();
                    } });
                }
            }
            // 목적지 판매대 (고양이 316) — 벽 속으로 출발해 한국어로 한 번 연 뒤 영어로 다시
            steps.Add(new Step { Name = "한국어로 벽 속 출발", Act = () => SetLang("ko") });
            steps.Add(new Step { Name = "벽 속 도착", Ready = () =>
            {
                if (StageUp() || EditorApplication.timeSinceStartup - _stepAt > 60) return true;
                var pad = Object.FindFirstObjectByType<DeparturePad>();
                if (pad != null && pad.IsSpawned && SceneManager.GetActiveScene().name == "Hub")
                {
                    for (int i = 0; i < 4 && !pad.DestinationName.Contains("벽 속"); i++) pad.ServerCycleDestination();
                    Put(pad.transform.position + Vector3.up * 0.5f);
                }
                return false;
            }, Check = () => StageUp() ? null : "벽 속 스테이지가 안 열림" });
            foreach (var lang in new[] { "ko", "en" })
            {
                var code = lang;
                steps.Add(new Step { Name = $"판매대 열기 ({code})", Wait = 1f, Act = () =>
                {
                    SetLang(code);
                    EventBus.RaiseWorldPanelRequested(WorldPanelKind.StageShop, Object.FindFirstObjectByType<StageShopCounter>());
                } });
                steps.Add(new Step { Name = $"판매대 글자 ({code})", Wait = 0.8f, Check = () =>
                {
                    var panel = Object.FindFirstObjectByType<StageShopPanel>();
                    if (panel == null || !panel.IsOpen) return "판매대 창이 안 열림";
                    ScanOverflow($"판매대·{code}", panel);
                    if (code == "en") ScanHangul($"판매대·{code}", panel);
                    panel.Close();
                    return null;
                } });
            }
            steps.Add(new Step { Name = "결과", Check = () =>
            {
                Report.Append($" | 넘침 {_overflow.Count} · 영어 한글 {_koreanLeft.Count}");
                foreach (var o in _overflow) Report.Append($" | 넘침 {o}");
                foreach (var k in _koreanLeft) Report.Append($" | 한글 {k}");
                return _overflow.Count == 0 && _koreanLeft.Count == 0 ? null : "창 글자 문제";
            } });
            return steps;
        }
    }
}
