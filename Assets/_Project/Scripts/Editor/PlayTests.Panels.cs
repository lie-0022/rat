using System.Collections.Generic;
using System.IO;
using RatGame.Core;
using RatGame.UI;
using RatGame.World;
using UnityEditor;
using UnityEngine;
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

        private static readonly string[] PanelNames = { "상점", "거울", "도감", "설정", "일시정지" };

        private static void OpenPanel(int i)
        {
            switch (i)
            {
                case 0: EventBus.RaiseWorldPanelRequested(WorldPanelKind.Shop, Object.FindFirstObjectByType<VendingMachine>()); break;
                case 1: EventBus.RaiseWorldPanelRequested(WorldPanelKind.Mirror, Object.FindFirstObjectByType<Mirror>()); break;
                case 2: EventBus.RaiseWorldPanelRequested(WorldPanelKind.Codex, Object.FindFirstObjectByType<CodexBook>()); break;
                case 3: Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include).Open(); break;
                case 4: Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include).Open(); break;
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
