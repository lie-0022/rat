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
    ///   -autostage N: -autohost와 함께 — 기지 대신 벽 속 스테이지 N으로 바로 (성능 측정용, 고양이 86)
    /// 릴리스 빌드에서는 인자가 있어도 아무것도 안 한다.
    /// </summary>
    public class DevAutoConnect : MonoBehaviour
    {
        private void Start()
        {
            // 릴리스에선 인자를 무시 — -autostage로 깊은 스테이지 도전과제를 건너뛰지 못하게 (고양이 322). 시험·성능 측정은 개발 빌드
            if (!Debug.isDebugBuild) return;
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
            int stage = AutoStageArg();
            if (task.Result && stage > 0)
            {
                RatGame.Run.RunSession.StageNumber = stage;
                RatGame.Run.RunSession.DepartPending = true; // 기지 발판 출발과 같은 길 — 로드 뒤 자동 출발·맵 생성
                NetworkManager.Singleton.SceneManager.LoadScene("Stage_Walls", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else if (task.Result)
                NetworkManager.Singleton.SceneManager.LoadScene("Hub",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            else
                Log.Error("[AutoConnect] 호스트 시작 실패");
        }

        private static int AutoStageArg()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autostage");
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int n) ? n : 0;
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
                    // 접속 확정(스폰)까지 잠시 관찰 — 실패하면 NGO가 Shutdown하므로 IsListening으로 판별.
                    // 새로 빌드한 앱의 첫 실행은 느려서(맥 첫 실행 검사) 3초로는 핸드셰이크 도중 끊고 다시 시도했다 → 6초
                    yield return new WaitForSeconds(6f);
                    if (NetworkManager.Singleton.IsConnectedClient) yield break;
                }
                Log.DevWarn($"[AutoConnect] 접속 재시도 {attempt}/5");
                yield return new WaitForSeconds(2f);
            }
            Log.Error("[AutoConnect] 접속 최종 실패");
        }
    }
}
