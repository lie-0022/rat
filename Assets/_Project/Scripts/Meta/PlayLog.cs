using System;
using System.Globalization;
using System.IO;
using System.Text;
using RatGame.Core;
using RatGame.Run;
using UnityEngine;

namespace RatGame.Meta
{
    /// <summary>
    /// 플레이 기록 CSV (docs/14 "플레이 통계 로컬 CSV 덤프 — 밸런싱 근거용", 고양이 330). 호스트만, 스테이지가 끝날 때(귀환·전멸) 한 줄:
    /// 날짜·스테이지·할당량·적립·들고 온 것·결과·걸린 초·인원·다운·오늘의 집·시드. persistentDataPath/playlog.csv — 친구 시험(docs/13 3-1) 뒤 엑셀로 연다.
    /// 싱글톤 금지(docs/02)라 스스로 생기는 컴포넌트, RunManager 페이즈만 읽는다. 시험 도구는 Temp로 돌리고(PathOverride), 측정 실행은 쓰지 않는다(SaveService.WritesDisabled).
    /// </summary>
    public class PlayLog : MonoBehaviour
    {
        public const string FileName = "playlog.csv";
        private const string Header = "날짜,스테이지,할당량,적립,들고온것,결과,초,인원,다운,오늘의집,시드";

        /// <summary>에디터 시험 도구가 Temp 아래로 돌린다 — 자동 시험이 사용자 기록에 줄을 보태지 않게, 통째 시험은 그 줄 수를 센다.</summary>
        public static string PathOverride;

        private RunPhase _last = RunPhase.Ready;
        private float _stageStart;
        private int _downs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PathOverride = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("PlayLog");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayLog>();
        }

        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        private void OnEnable() => EventBus.PlayerDowned += OnDowned;
        private void OnDisable() => EventBus.PlayerDowned -= OnDowned;
        private void OnDowned(ulong _) => _downs++;

        private void Update()
        {
            var run = RunManager.Instance;
            if (run == null || !run.IsSpawned || !run.IsServer) { _last = RunPhase.Ready; return; }
            var phase = run.Phase.Value;
            if (phase == _last) return;
            var prev = _last;
            _last = phase;
            if (phase == RunPhase.StageActive) { _stageStart = Time.time; _downs = 0; }
            else if ((phase == RunPhase.Returned || phase == RunPhase.Wiped) && (prev == RunPhase.StageActive || prev == RunPhase.Returning))
                Write(run, phase == RunPhase.Wiped);
        }

        private void Write(RunManager run, bool wiped)
        {
            if (SaveService.WritesDisabled) return;
            var quota = FindFirstObjectByType<StageQuota>();
            bool finished = quota != null && quota.Finished.Value;
            string result = wiped ? "전멸" : finished ? "엔딩" : "클리어";
            int players = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count : 1;
            string line = Row(DateTime.Now, quota != null ? quota.StageNumber.Value : 0, quota != null ? quota.Quota.Value : 0,
                run.StashedValue.Value, wiped ? 0 : run.ResultCarriedValue.Value, result, Time.time - _stageStart, players, _downs,
                quota != null ? ((StageModifier)quota.Modifier.Value).ToString() : "", run.RunSeed.Value);
            try
            {
                bool fresh = !File.Exists(FilePath);
                // 새 파일은 BOM + 머리줄 — 엑셀이 한글을 UTF-8로 읽게
                if (fresh) File.WriteAllText(FilePath, Header + "\n", new UTF8Encoding(true));
                File.AppendAllText(FilePath, line + "\n", new UTF8Encoding(false));
                Log.Dev($"플레이 기록: {line}");
            }
            catch (Exception e)
            {
                Log.DevWarn($"플레이 기록 쓰기 실패: {e.Message}");
            }
        }

        /// <summary>한 줄 — 시험에서도 부른다. 소수점은 문화권과 상관없이 점.</summary>
        public static string Row(DateTime at, int stage, int quota, int stashed, int carried, string result, float seconds,
                                 int players, int downs, string modifier, int seed) =>
            string.Join(",", at.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), stage, quota, stashed, carried, result,
                seconds.ToString("0", CultureInfo.InvariantCulture), players, downs, modifier, seed);
    }
}
