using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 임시 IMGUI 네트워크 패널 — 진짜 메뉴 UI(태스크 2-6)가 생기면 삭제한다.
    /// MPPM 가상 플레이어에서도 버튼만으로 호스트/참가할 수 있게 하는 게 목적.
    /// 호스트는 접속 후 Sandbox_Net을 로드한다 (docs/03 — 씬은 호스트가 로드, 클라 자동 동기화).
    /// </summary>
    public class DevNetHud : MonoBehaviour
    {
        // 호스트 Enter = 스테이지 출발. 버튼은 Esc로 커서를 풀어야 눌러져 테스트가 번거롭다
        private void Update()
        {
            var nm = NetworkManager.Singleton;
            var run = Run.RunManager.Instance;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (nm == null || !nm.IsHost || run == null || kb == null) return;
            if (run.Phase.Value != Run.RunPhase.Ready) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                run.ServerStartStage(Random.Range(0, int.MaxValue));
        }

        private void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 200), GUI.skin.box);
            if (!nm.IsListening)
            {
                GUILayout.Label("[DEV] 넷 테스트");
                if (GUILayout.Button("Host + Sandbox_Net")) StartHostToSandbox();
                if (GUILayout.Button("Join 127.0.0.1")) _ = NetworkLauncher.Instance.JoinAsync();
            }
            else
            {
                string role = nm.IsHost ? "Host" : "Client";
                GUILayout.Label($"[DEV] {role} — 접속 {nm.ConnectedClientsIds.Count}명");
                if (GUILayout.Button("Shutdown")) NetworkLauncher.Instance.Shutdown();

                var run = Run.RunManager.Instance;
                if (run != null)
                {
                    GUILayout.Label($"런: {run.Phase.Value} 스테이지{run.StageNumber.Value} 쥐구멍{run.StashedValue.Value} 누계{run.RunTotalValue.Value}");
                    if (nm.IsHost && run.Phase.Value == Run.RunPhase.Ready && GUILayout.Button("출발"))
                        run.ServerStartStage(Random.Range(0, int.MaxValue));
                }
            }
            GUILayout.EndArea();
        }

        private async void StartHostToSandbox()
        {
            bool ok = await NetworkLauncher.Instance.StartHostAsync();
            if (ok)
                NetworkManager.Singleton.SceneManager.LoadScene("Sandbox_Net",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
