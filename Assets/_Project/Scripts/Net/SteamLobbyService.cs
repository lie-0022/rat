using System;
using System.Threading.Tasks;
using RatGame.Core;
using Steamworks;
using Steamworks.Data;

namespace RatGame.Net
{
    /// <summary>
    /// Steam 클라이언트 수명 + 친구 로비 (docs/03 SteamLobbyService, 태스크 0-6). NetworkLauncher가 소유한다 — 싱글톤 아님.
    /// 호스트 = 친구 전용 로비 생성, 참가 = 초대 수락·친구 목록 "게임 참가"(OnGameLobbyJoinRequested) → 로비 입장 → 방장 SteamId로 NGO 접속.
    /// Steam이 없거나 로그인 안 돼 있으면 IsAvailable=false — 게임은 UnityTransport 로컬 모드로 계속 (docs/03).
    /// </summary>
    public sealed class SteamLobbyService : IDisposable
    {
        /// <summary>개발용 공용 AppID(Spacewar). 정식 출시 때 전용 AppID로 바꾼다 (docs/03).</summary>
        public const uint DevAppId = 480;

        // 480은 여러 게임이 같이 쓰므로 우리 로비임을 표시 (로비 검색을 붙일 때 필터로 쓴다)
        private const string GameKey = "rat_game";
        private const string GameValue = "1";
        private const string ConnectLobbyArg = "+connect_lobby";

        private Lobby? _lobby;

        public bool IsAvailable { get; private set; }
        public string PersonaName => IsAvailable ? SteamClient.Name : "";
        public ulong MySteamId => IsAvailable ? SteamClient.SteamId.Value : 0;
        public ulong CurrentLobbyId => _lobby.HasValue ? _lobby.Value.Id.Value : 0;

        /// <summary>초대 수락·친구 목록 참가 요청 (로비 Id, 초대한 친구 이름).</summary>
        public event Action<ulong, string> JoinRequested;
        private SteamAchievementBridge _achievements;

        public bool TryInit()
        {
            try
            {
                if (!SteamClient.IsValid) SteamClient.Init(DevAppId, false);
                IsAvailable = SteamClient.IsValid && SteamClient.IsLoggedOn;
            }
            catch (Exception e)
            {
                Log.DevWarn($"Steam 초기화 실패 — 로컬 모드로 계속: {e.Message}");
                IsAvailable = false;
            }
            if (!IsAvailable) return false;

            SteamNetworkingUtils.InitRelayNetworkAccess();
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            Log.Dev($"Steam 연결: {SteamClient.Name}");
            // 도전과제 연동 (고양이 326) — 개발 AppID 480(Spacewar)은 남의 앱 통계라 건드리지 않는다
            if (SteamClient.AppId.Value != DevAppId)
            {
                _achievements = new SteamAchievementBridge(new SteamAchievementSink());
                int synced = _achievements.SyncAll(SaveService.Data.CompletedAchievementIds);
                _achievements.Attach();
                Log.Dev($"Steam 도전과제 맞춤: {synced}개");
            }
            return true;
        }

        /// <summary>매 프레임 — 비동기 콜백 모드를 끄고 초기화했으므로 직접 돌려야 초대·로비 이벤트가 온다.</summary>
        public void Tick()
        {
            if (IsAvailable) SteamClient.RunCallbacks();
        }

        public async Task<bool> CreateLobbyAsync(int maxMembers)
        {
            LeaveLobby();
            var created = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            if (!created.HasValue) return false;

            var lobby = created.Value;
            lobby.SetFriendsOnly();
            lobby.SetJoinable(true);
            lobby.SetData(GameKey, GameValue);
            _lobby = lobby;
            Log.Dev($"Steam 로비 생성: {lobby.Id.Value}");
            return true;
        }

        /// <summary>로비에 들어가 방장 SteamId를 돌려준다. 실패하면 0.</summary>
        public async Task<ulong> JoinLobbyAsync(ulong lobbyId)
        {
            LeaveLobby();
            var lobby = new Lobby(lobbyId);
            var result = await lobby.Join();
            if (result != RoomEnter.Success)
            {
                Log.DevWarn($"Steam 로비 입장 실패 ({lobbyId}): {result}");
                return 0;
            }
            _lobby = lobby;
            Log.Dev($"Steam 로비 입장: {lobbyId} (방장 {lobby.Owner.Name})");
            return lobby.Owner.Id.Value;
        }

        public void LeaveLobby()
        {
            if (!_lobby.HasValue) return;
            _lobby.Value.Leave();
            _lobby = null;
        }

        /// <summary>현재 로비로 친구 초대 창 (Steam 오버레이).</summary>
        public void OpenInviteOverlay()
        {
            if (_lobby.HasValue) SteamFriends.OpenGameInviteOverlay(_lobby.Value.Id);
        }

        /// <summary>
        /// 오버레이 없이 로비 초대 보내기 — Steam으로 실행하지 않은 빌드·에디터는 오버레이가 꺼져 있다(SteamUtils.IsOverlayEnabled).
        /// 받은 친구는 Steam 채팅의 초대를 수락하면 JoinRequested가 온다(게임이 켜져 있을 때).
        /// </summary>
        public bool InviteFriend(ulong friendId)
        {
            if (!_lobby.HasValue) return false;
            bool ok = _lobby.Value.InviteFriend(friendId);
            Log.Dev($"Steam 초대 보냄: {new Friend(friendId).Name} ({(ok ? "성공" : "실패")})");
            return ok;
        }

        /// <summary>온라인 친구 (초대 목록용). 이름순.</summary>
        public System.Collections.Generic.List<(ulong id, string name)> GetOnlineFriends()
        {
            var list = new System.Collections.Generic.List<(ulong id, string name)>();
            if (!IsAvailable) return list;
            foreach (var f in SteamFriends.GetFriends())
                if (f.IsOnline) list.Add((f.Id.Value, f.Name));
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.CurrentCulture));
            return list;
        }

        public bool IsOverlayEnabled => IsAvailable && SteamUtils.IsOverlayEnabled;

        public void OpenFriendsOverlay()
        {
            if (IsAvailable) SteamFriends.OpenOverlay("friends");
        }

        /// <summary>게임이 꺼진 상태에서 초대를 수락하면 Steam이 "+connect_lobby &lt;id&gt;"로 실행한다. 없으면 0.</summary>
        public static ulong ParseConnectLobbyArg(string[] args)
        {
            int i = Array.IndexOf(args, ConnectLobbyArg);
            return i >= 0 && i + 1 < args.Length && ulong.TryParse(args[i + 1], out ulong id) ? id : 0;
        }

        private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friend)
        {
            JoinRequested?.Invoke(lobby.Id.Value, new Friend(friend).Name);
        }

        public void Dispose()
        {
            LeaveLobby();
            _achievements?.Dispose();
            _achievements = null;
            if (!IsAvailable) return;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
            SteamClient.Shutdown();
            IsAvailable = false;
        }
    }
}
