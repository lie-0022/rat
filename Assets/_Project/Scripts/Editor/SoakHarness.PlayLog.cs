using System.IO;
using RatGame.Meta;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 통째 시험이 플레이 기록 CSV도 확인 (고양이 330) — 사용자 playlog.csv 대신 Temp로 돌리고, 끝나면 스테이지마다 한 줄씩 적혔는지 센다.
    /// </summary>
    public static partial class SoakHarness
    {
        private static readonly string PlayLogPath = Path.GetFullPath("Temp/playlog-soak.csv");

        private static void StartPlayLog()
        {
            if (File.Exists(PlayLogPath)) File.Delete(PlayLogPath);
            PlayLog.PathOverride = PlayLogPath;
        }

        /// <summary>결과 한 조각. 성공한 판에서 줄 수가 스테이지 수와 다르면 failure를 채운다.</summary>
        private static string CheckPlayLog(ref string failure)
        {
            string[] lines = File.Exists(PlayLogPath) ? File.ReadAllLines(PlayLogPath) : new string[0];
            int rows = System.Math.Max(0, lines.Length - 1); // 머리줄 빼고
            int endings = 0;
            foreach (var l in lines) if (l.Contains(",엔딩,")) endings++;
            if (failure == null && rows != _stagesSeen) failure = $"플레이 기록 {rows}줄 — 스테이지 {_stagesSeen}개와 다름";
            if (failure == null && (_lootEmbedded > 0 || _lootSunk > 0)) failure = $"물건이 벽·가구에 끼어 남 {_lootEmbedded} · 바닥에 묻혀 남 {_lootSunk} (고양이 341)";
            return $"플레이 기록 {rows}줄(엔딩 {endings}) | {EmbeddedReport()}";
        }
    }
}
