using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 임시 IMGUI 네트워크 패널 — 진짜 메뉴 UI(태스크 2-6)가 생기면 삭제한다.
    /// MPPM 가상 플레이어에서도 버튼만으로 호스트/참가할 수 있게 하는 게 목적.
    /// 호스트는 시작 후 기지(Hub)를 로드한다 (docs/03 — 씬은 호스트가 로드, 클라 자동 동기화; docs/11 — 런은 기지에서 출발).
    /// </summary>
    public class DevNetHud : MonoBehaviour
    {
        // 개발용: 에디터에서 스테이지 씬을 직접 플레이할 때만 쓰는 출발 (정식 흐름은 기지 출발 발판 — docs/11).
        // 버튼은 Esc로 커서를 풀어야 눌러져 테스트가 번거로워 Enter도 받는다
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
            // 접속 전 호스트/참가는 메인 메뉴(UI/MainMenuController)가 맡는다 — 여기선 세션 중 정보·종료만
            if (nm == null || !nm.IsListening)
            {
                RatGame.Core.InputFocus.NoRelockGuiRect = default;
                return;
            }

            // 세션 나가기는 일시정지 창(Esc)이 맡는다 — 여기선 개발 정보·개발용 출발만
            var area = new Rect(10, 10, 220, 110);
            RatGame.Core.InputFocus.NoRelockGuiRect = area; // 이 패널 위 클릭은 커서 재잠금으로 먹지 않게
            GUILayout.BeginArea(area, GUI.skin.box);
            {
                string role = nm.IsHost ? $"Host — 접속 {nm.ConnectedClientsIds.Count}명" : "Client";
                GUILayout.Label($"[DEV] {role}");

                var run = Run.RunManager.Instance;
                if (run != null)
                {
                    GUILayout.Label($"런: {run.Phase.Value} 쥐구멍{run.StashedValue.Value} 누계{run.RunTotalValue.Value}");
                    if (nm.IsHost && run.Phase.Value == Run.RunPhase.Ready && GUILayout.Button("출발 (개발용)"))
                        run.ServerStartStage(Random.Range(0, int.MaxValue));
                }
            }
            GUILayout.EndArea();
        }
    }
}
