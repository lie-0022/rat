using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RatGame.Core;
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
    /// 플레이 도중 언어 바꾸기 (고양이 302) — 한국어로 시작해 기지·창고에서 English로 바꾼 뒤 1.5초, 화면의 글자에 한글이 남는지.
    /// 통째 영어 시험은 처음부터 영어라 "바뀔 때만 다시 쓰는" 표시를 못 잡는다(301 내 소리 표시). 설정 파일은 바이트 그대로 되돌린다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Switch Language Mid-Play")]
        private static void ArmLanguage() => Arm("langswitch");

        private static readonly Regex HangulRx = new("[가-힣]");
        private static readonly List<string> _koreanLeft = new();

        private static void SetLang(string code)
        {
            var d = SettingsService.Current.Clone(); d.Language = code; SettingsService.Apply(d);
        }

        private static void ScanHangul(string where)
        {
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || !HangulRx.IsMatch(t.text) || t.text.StartsWith("[DEV]")) continue;
                if (t.GetComponentInParent<CatDebugOverlay>() != null || t.GetComponentInParent<DevCheckMenu>() != null) continue;
                bool hidden = false;
                foreach (var g in t.GetComponentsInParent<CanvasGroup>()) if (g.alpha < 0.01f) hidden = true;
                if (hidden) continue;
                string s = $"{where}: {t.transform.parent?.name}/{t.name} \"{t.text.Replace("\n", " / ")}\"";
                if (!_koreanLeft.Contains(s)) _koreanLeft.Add(s);
            }
        }

        private static readonly List<string> _overflow = new();

        // 글자가 칸을 넘치는지 (TMP isTextOverflowing) — 영어가 한국어보다 길어 잘리거나 삐져나오는 곳 (고양이 313)
        private static void ScanOverflow(string where)
        {
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || t.text.StartsWith("[DEV]")) continue;
                if (t.GetComponentInParent<CatDebugOverlay>() != null || t.GetComponentInParent<DevCheckMenu>() != null) continue;
                t.ForceMeshUpdate();
                if (!t.isTextOverflowing) continue;
                string s = $"{where}: {t.transform.parent?.name}/{t.name} \"{t.text.Replace("\n", " / ")}\"";
                if (!_overflow.Contains(s)) _overflow.Add(s);
            }
        }

        private static List<Step> LanguageSteps()
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
                    var prev = RestoreAfterPlay; RestoreAfterPlay = () => { prev?.Invoke(); restore(); }; // 멈춘 뒤에도
                    OnDone = () => { SetLang("ko"); restore(); };
                    SetLang("ko");
                } },
            };
            // 메인 메뉴에서도 (고양이 312)
            steps.Add(new Step { Name = "메뉴에서 영어로", Wait = 1f, Ready = () => Object.FindFirstObjectByType<MainMenuController>() != null, Act = () => SetLang("en") });
            steps.Add(new Step { Name = "메뉴 글자", Wait = 1.5f, Act = () => { ScanHangul("메뉴"); SetLang("ko"); } });
            steps.AddRange(ToWarehouse()); // 메뉴 → 호스트 → 기지 → 창고 (언어는 한국어로)
            steps.Add(new Step { Name = "창고에서 영어로", Wait = 2f, Act = () => SetLang("en") });
            steps.Add(new Step { Name = "창고 글자", Wait = 1.5f, Check = () =>
            {
                ScanHangul("창고");
                ScanOverflow("창고");
                Report.Append($" | 메뉴·창고 한글 남음 {_koreanLeft.Count}");
                foreach (var s in _koreanLeft) Report.Append($" | {s}");
                return _koreanLeft.Count == 0 ? null : "영어로 바꿨는데 한글이 남음";
            } });
            // 귀환해서 결과 화면도 (영어) — 한글·넘침
            steps.Add(new Step { Name = "귀환", Act = () =>
            {
                var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                Put(zone.center + Vector3.up * 0.2f);
            } });
            steps.Add(new Step { Name = "결과 화면 글자", Wait = 1f, Ready = () => RunManager.Instance != null && RunManager.Instance.IsShowingResult || EditorApplication.timeSinceStartup - _stepAt > 15, Check = () =>
            {
                if (RunManager.Instance == null || !RunManager.Instance.IsShowingResult) return "결과 화면이 안 뜸";
                int before = _koreanLeft.Count;
                ScanHangul("결과");
                ScanOverflow("결과");
                Report.Append($" | 결과 한글 {_koreanLeft.Count - before} · 넘침 {_overflow.Count}");
                foreach (var o in _overflow) Report.Append($" | 넘침 {o}");
                if (_koreanLeft.Count != before) return "결과 화면에 한글";
                return _overflow.Count == 0 ? null : "영어 글자가 칸을 넘침";
            } });
            return steps;
        }
    }
}
