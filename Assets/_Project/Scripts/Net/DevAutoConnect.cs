using System;
using System.Collections;
using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Net
{
    /// <summary>
    /// 커맨드라인 자동 접속 — 무인 멀티 테스트용 (사람 없이 4인 접속 검증).
    ///   -autohost   : 부팅 후 호스트 시작 + 기지(Hub) 로드
    ///   -autojoin   : 부팅 후 127.0.0.1 접속 (재시도 5회)
    ///   -autowander : 스폰된 자기 플레이어가 자동 배회 (이동 동기화 검증용)
    /// 릴리즈 빌드에서는 스트립까진 안 하지만 인자 없으면 아무것도 안 한다.
    /// </summary>
    public class DevAutoConnect : MonoBehaviour
    {
        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool host = Array.IndexOf(args, "-autohost") >= 0;
            bool join = Array.IndexOf(args, "-autojoin") >= 0;
            if (Array.IndexOf(args, "-autowander") >= 0)
                RatGame.Player.PlayerController.DevAutoWander = true;

            if (host) StartCoroutine(AutoHost());
            else if (join) StartCoroutine(AutoJoin());
        }

        private IEnumerator AutoHost()
        {
            yield return new WaitForSeconds(1f); // 부트스트랩(MainMenu 로드) 완료 대기
            var task = NetworkLauncher.Instance.StartHostAsync();
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.Result)
                NetworkManager.Singleton.SceneManager.LoadScene("Hub",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            else
                Log.Error("[AutoConnect] 호스트 시작 실패");
        }

        private IEnumerator AutoJoin()
        {
            yield return new WaitForSeconds(2f); // 호스트가 먼저 뜰 시간
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                var task = NetworkLauncher.Instance.JoinAsync();
                yield return new WaitUntil(() => task.IsCompleted);
                if (task.Result)
                {
                    // 접속 확정(스폰)까지 잠시 관찰 — 실패하면 NGO가 Shutdown하므로 IsListening으로 판별
                    yield return new WaitForSeconds(3f);
                    if (NetworkManager.Singleton.IsConnectedClient) yield break;
                }
                Log.DevWarn($"[AutoConnect] 접속 재시도 {attempt}/5");
                yield return new WaitForSeconds(2f);
            }
            Log.Error("[AutoConnect] 접속 최종 실패");
        }
    }
}
