using System.Net;
using System.Net.Sockets;
using RatGame.Net;
using UnityEditor;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 시험 도구용 포트 고르기 (고양이 262). 플레이 중 컴파일로 UDP 7777이 에디터에 새면 에디터 재시작 전까지 호스트가 안 떴다 —
    /// 시험을 시작할 때 7777이 막혀 있으면 7778을 쓰고, 빌드 클라에도 -port로 같은 값을 준다. 게임 기본값(7777)은 그대로.
    /// </summary>
    public static class DevPort
    {
        /// <summary>시험 시작 때 부른다 — 쓸 포트를 SessionState에 적는다.</summary>
        public static int Choose()
        {
            int port = Free(7777) ? 0 : Free(7778) ? 7778 : 7779;
            SessionState.SetInt(NetworkLauncher.DevPortKey, port);
            if (port != 0) UnityEngine.Debug.Log($"[Rat] 시험 포트: 7777이 막혀 있어 {port} 사용 (에디터를 다시 켜면 풀림)");
            return port;
        }

        /// <summary>빌드 클라 실행 인자 — 기본 포트면 빈 문자열.</summary>
        public static string ClientArg
        {
            get { int p = SessionState.GetInt(NetworkLauncher.DevPortKey, 0); return p > 0 ? $" -port {p}" : ""; }
        }

        private static bool Free(int port)
        {
            try { using var u = new UdpClient(new IPEndPoint(IPAddress.Loopback, port)); return true; }
            catch (SocketException) { return false; }
        }
    }
}
