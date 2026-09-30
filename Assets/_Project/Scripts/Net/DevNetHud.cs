using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 개발용 IMGUI 네트워크 패널 (에디터·개발 빌드만). 오버레이 없는 환경의 친구 목록 초대는 여기만 있다(docs/03 원격 시험 순서).
    /// MPPM 가상 플레이어에서도 버튼만으로 호스트/참가할 수 있게 하는 게 목적.
    /// 호스트는 시작 후 기지(Hub)를 로드한다 (docs/03 — 씬은 호스트가 로드, 클라 자동 동기화; docs/11 — 런은 기지에서 출발).
    /// </summary>
    public class DevNetHud : MonoBehaviour
    {
        // 릴리스에선 끈다 — "[DEV]" 상자가 플레이어 화면에 뜨고, 매 프레임 IMGUI가 약 3.9KB를 할당했다 (고양이 324·325).
        // 초대는 일시정지 창 "친구 초대"(Steam 오버레이)로
        private void Awake()
        {
            if (!Debug.isDebugBuild) enabled = false;
        }

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
            // 좌하단 — 좌상단은 팀 상태 HUD 자리
            var launcher = NetworkLauncher.Instance;
            bool steam = launcher != null && launcher.UsingSteam;
            float height = steam ? 172 : 132; // 성능 줄 한 줄 포함 (고양이 86)
            var area = new Rect(10, Screen.height - height - 10, 240, height);
            RatGame.Core.InputFocus.NoRelockGuiRect = area; // 이 패널 위 클릭은 커서 재잠금으로 먹지 않게
            GUILayout.BeginArea(area, GUI.skin.box);
            {
                string role = nm.IsHost ? $"Host — 접속 {nm.ConnectedClientsIds.Count}명" : "Client";
                GUILayout.Label($"[DEV] {role}");
                if (steam)
                {
                    var svc = launcher.Steam;
                    GUILayout.Label($"Steam 로비 {svc.CurrentLobbyId}");
                    // Steam으로 실행하지 않으면 오버레이가 꺼져 있다 — 그땐 온라인 친구에게 직접 초대
                    if (svc.IsOverlayEnabled)
                    {
                        if (GUILayout.Button("친구 초대 (Steam 오버레이)")) svc.OpenInviteOverlay();
                    }
                    else if (GUILayout.Button(_showFriends ? "친구 목록 닫기" : "친구 초대 (목록)"))
                    {
                        _showFriends = !_showFriends;
                        if (_showFriends) _friends = svc.GetOnlineFriends();
                    }
                }

                if (DevPerfProbe.HasData) GUILayout.Label($"FPS {1000f / DevPerfProbe.AvgMs:F0} · 평균 {DevPerfProbe.AvgMs:F1}ms · 1% {DevPerfProbe.Worst1Ms:F1}ms");
                var run = Run.RunManager.Instance;
                if (run != null)
                {
                    GUILayout.Label($"런: {run.Phase.Value} 쥐구멍{run.StashedValue.Value} 누계{run.RunTotalValue.Value}");
                    if (nm.IsHost && run.Phase.Value == Run.RunPhase.Ready && GUILayout.Button("출발 (개발용)"))
                        run.ServerStartStage(Random.Range(0, int.MaxValue));
                }
            }
            GUILayout.EndArea();

            if (steam && _showFriends) DrawFriendList(launcher.Steam, area);
        }

        private bool _showFriends;
        private System.Collections.Generic.List<(ulong id, string name)> _friends = new();

        // 패널 오른쪽에 온라인 친구 — 누르면 로비 초대 (상대는 Steam 채팅에서 수락)
        private void DrawFriendList(SteamLobbyService svc, Rect anchor)
        {
            float height = 30 + 24 * Mathf.Max(1, _friends.Count);
            var box = new Rect(anchor.xMax + 6, anchor.yMax - height, 220, height);
            RatGame.Core.InputFocus.NoRelockGuiRect = new Rect(anchor.x, box.y, box.xMax - anchor.x, anchor.yMax - box.y);
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label("온라인 친구 — 눌러서 초대");
            if (_friends.Count == 0) GUILayout.Label("(온라인 친구 없음)");
            foreach (var f in _friends)
                if (GUILayout.Button(f.name)) svc.InviteFriend(f.id);
            GUILayout.EndArea();
        }
    }
}
