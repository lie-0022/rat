using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatGame.Net
{
    /// <summary>
    /// 릴리스 빌드 연기 시험 (고양이 336) — `-smokehost`: 메인 메뉴 → 호스트 → 기지 15초 → 발판으로 벽 속 스테이지 1 → 15초, 그동안 오류·예외를 센 뒤 결과 한 줄을 남기고 끈다
    /// (종료 코드 0 = 통과). 릴리스는 개발 도구(-autohost 등)를 닫아서 호스트까지 켜 본 적이 없었다 — 코드 스트리핑·개발 전용 분기 차이를 잡으려고.
    /// 스테이지 1은 보통 출발과 같은 길이라 건너뛰기(도전과제)와 무관, 세이브도 안 쓴다(SaveService). 로그는 Log.Dev가 빠진 릴리스에서도 보이게 Debug.Log.
    /// </summary>
    public class ReleaseSmoke : MonoBehaviour
    {
        private const float WatchSeconds = 15f;
        private int _errors;
        private string _firstError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-smokehost") < 0) return;
            var go = new GameObject("ReleaseSmoke");
            DontDestroyOnLoad(go);
            go.AddComponent<ReleaseSmoke>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string message, string stack, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            _errors++;
            _firstError ??= message.Split('\n')[0];
        }

        private IEnumerator Start()
        {
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "MainMenu" && NetworkLauncher.Instance != null || Time.realtimeSinceStartup - t0 > 20f);
            yield return new WaitForSeconds(1f);
            var task = NetworkLauncher.Instance.StartHostAsync();
            yield return new WaitUntil(() => task.IsCompleted);
            if (!task.Result) { Finish("호스트 시작 실패"); yield break; }
            NetworkManager.Singleton.SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            t0 = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Hub" && NetworkManager.Singleton.LocalClient?.PlayerObject != null || Time.realtimeSinceStartup - t0 > 20f);
            if (SceneManager.GetActiveScene().name != "Hub" || NetworkManager.Singleton.LocalClient?.PlayerObject == null) { Finish("기지·내 쥐가 안 섬"); yield break; }
            yield return new WaitForSeconds(WatchSeconds);
            if (_errors > 0) { Finish($"기지에서 오류 {_errors} (첫: {_firstError})"); yield break; }

            // 벽 속 스테이지 1로 — 보통 플레이처럼 발판에 올라 출발 (맵 생성이 릴리스에서 가장 큰 코드 길)
            var pad = FindFirstObjectByType<RatGame.World.DeparturePad>();
            if (pad == null) { Finish("출발 발판 없음"); yield break; }
            for (int i = 0; i < 4 && !pad.DestinationName.Contains("벽 속"); i++) pad.ServerCycleDestination();
            t0 = Time.realtimeSinceStartup;
            while (!(SceneManager.GetActiveScene().name == "Stage_Walls" && Run.RunManager.Instance != null && Run.RunManager.Instance.IsSpawned
                     && Run.RunManager.Instance.Phase.Value == Run.RunPhase.StageActive))
            {
                if (Time.realtimeSinceStartup - t0 > 40f) { Finish("벽 속 스테이지가 안 열림"); yield break; }
                var me = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (me != null && SceneManager.GetActiveScene().name == "Hub" && pad != null) Put(me.transform, pad.transform.position + Vector3.up * 0.5f);
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(WatchSeconds);
            Finish(_errors == 0 ? null : $"스테이지에서 오류 {_errors} (첫: {_firstError})");
        }

        private static void Put(Transform t, Vector3 pos)
        {
            var body = t.GetComponent<Rigidbody>();
            if (body != null) { body.position = pos; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
            t.position = pos;
        }

        private void Finish(string failure)
        {
            Debug.Log(failure == null ? $"[Rat] 스모크 통과 — 호스트·기지·벽 속 스테이지 1 각 {WatchSeconds:0}초 오류 0 (v{Application.version}, 개발 빌드 {Debug.isDebugBuild})"
                                      : $"[Rat] 스모크 실패 — {failure}");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            Application.Quit(failure == null ? 0 : 1);
        }
    }
}
