using RatGame.Net;
using UnityEditor;

namespace RatGame.Editor
{
    /// <summary>
    /// 에디터 네트워크 설정 (docs/03). 에디터 기본은 UnityTransport(로컬) — Steam 초대·로비를 에디터에서 시험할 때만 켠다.
    /// 켜려면 이 Mac에서 Steam 앱이 로그인돼 있어야 한다. 플레이 시작 시점에 읽는다.
    /// </summary>
    public static class NetTools
    {
        private const string MenuPath = "Tools/RatGame/Net/Use Steam In Editor";

        [MenuItem(MenuPath)]
        private static void ToggleSteam()
        {
            bool next = !EditorPrefs.GetBool(NetworkLauncher.EditorSteamPrefKey, false);
            EditorPrefs.SetBool(NetworkLauncher.EditorSteamPrefKey, next);
            UnityEngine.Debug.Log($"[Rat] 에디터 트랜스포트: {(next ? "Steam" : "UnityTransport")} (다음 플레이부터)");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleSteamValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(NetworkLauncher.EditorSteamPrefKey, false));
            return !EditorApplication.isPlaying;
        }
    }
}
