using RatGame.Core;
using Unity.Netcode;

namespace RatGame.Net
{
    /// <summary>접속 승인 — 정원·진행 중·맵 이동 중 거절 (docs/03). NetworkLauncher.cs에서 옮김 (고양이 228).</summary>
    public partial class NetworkLauncher
    {
        // ConnectionApproval (docs/03): 최대 4명, 게임 진행 중(midgame) 참가 거부. 스폰은 NetPlayerSpawner 수동.
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
                                       NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;

            if (NetworkManager.Singleton.ConnectedClientsIds.Count >= MaxPlayers)
            {
                response.Approved = false;
                response.Reason = "정원 초과 (최대 4명)";
                return;
            }
            if (GameStateMachine.Instance.Current == GameState.InRun)
            {
                response.Approved = false;
                response.Reason = "게임 진행 중에는 참가할 수 없음 (로비에서만 합류)";
                return;
            }
            // 다음 맵을 불러오는 동안(스테이지 사이·발판 출발 직후)은 상태가 잠깐 Lobby라 위 검사를 지나친다 —
            // 그때 들어오면 맵 불러오기와 겹쳐 물체 동기화가 10초 넘게 밀려 접속이 실패했다 (고양이 142)
            if (Run.RunSession.DepartPending)
            {
                response.Approved = false;
                response.Reason = "다음 맵으로 이동 중 — 기지로 돌아오면 참가할 수 있어요";
                return;
            }
            response.Approved = true;
        }
    }
}
