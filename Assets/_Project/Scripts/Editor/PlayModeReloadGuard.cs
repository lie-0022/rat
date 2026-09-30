using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 플레이 중 스크립트 컴파일(도메인 리로드) 직전에 네트워크를 닫는다 (고양이 282).
    /// 안 닫으면 UnityTransport 소켓(UDP 7777)이 에디터 프로세스에 남아 에디터를 다시 켤 때까지 호스트가 안 떴다(09-14 두 번, 09-30 한 번).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeReloadGuard
    {
        static PlayModeReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }

        private static void BeforeReload()
        {
            if (!EditorApplication.isPlaying) return;
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsListening) nm.Shutdown(true);
            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null) utp.Shutdown();
            Debug.LogWarning("[Rat] 플레이 중 컴파일 — 소켓이 새지 않게 네트워크를 먼저 닫음 (다시 하려면 플레이를 새로)");
        }
    }
}
