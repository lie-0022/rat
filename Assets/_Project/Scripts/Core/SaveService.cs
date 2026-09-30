using System;
using System.IO;
using UnityEngine;

namespace RatGame.Core
{
    /// <summary>저장 데이터 (docs/11). Version으로 마이그레이션 대비. 개인 메타 필드(코인·스킨)는 메타 단계에서 추가.</summary>
    [Serializable]
    public class SaveData
    {
        public int Version = 1;
        /// <summary>팀 누계 — 귀환으로 가져온 가치의 합. 호스트 기기에만 쌓인다 (세션 주인 기준, 레포식).</summary>
        public int HaulTotal;
        /// <summary>팀 업그레이드 레벨 (docs/11 상점) — 누계로 사고 누계와 같은 파일에 산다.</summary>
        public RatGame.Data.UpgradeLevels Upgrades;
        /// <summary>개인: 착용 스킨 Id (빈 문자열 = 기본 팀 색).</summary>
        public string EquippedSkinId = "";
        /// <summary>개인: 팔레트로 고른 털 색 "#RRGGBB" (빈 문자열 = 안 고름). 스킨과 둘 중 나중에 고른 것만 남는다.</summary>
        public string BodyColorHex = "";
        /// <summary>팀(호스트 저장): 새 루프에서 클리어한 가장 높은 스테이지, 엔딩 본 횟수 (고양이 74).</summary>
        public int BestStage;
        public int Endings;
        /// <summary>개인: 도감 해금 아이템 Id (첫 정산 시 호스트 ClientRpc → 각자 저장, docs/08).</summary>
        public System.Collections.Generic.List<string> UnlockedCodexIds = new();
        /// <summary>개인: 도전과제 통계(키 → 값)와 달성한 도전과제 Id (docs/11, 고양이 219). JsonUtility는 사전을 못 써서 목록.</summary>
        public System.Collections.Generic.List<StatEntry> Stats = new();
        public System.Collections.Generic.List<string> CompletedAchievementIds = new();
    }

    [System.Serializable]
    public class StatEntry
    {
        public string Key;
        public int Value;
    }

    /// <summary>
    /// 로컬 JSON 저장 (docs/02 허용 싱글톤 — 정적). persistentDataPath/save.json + 직전본 save.bak.
    /// 처음 Data에 접근할 때 읽고, Save()는 호출 즉시 쓴다 (지금은 귀환 정산 1회라 debounce 불필요).
    /// </summary>
    public static class SaveService
    {
        private const string FileName = "save.json";
        private const string BackupName = "save.bak";

        private static SaveData _data;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);

        public static void Load()
        {
            _data = TryRead(FilePath) ?? TryRead(BackupPath) ?? new SaveData();
            Log.Dev($"저장 불러오기: 누계 {_data.HaulTotal} ({FilePath})");
        }

        public static void Save()
        {
            if (_data == null) return;
            try
            {
                string json = JsonUtility.ToJson(_data, true);
                // 프로세스마다 다른 임시 이름 — 같은 컴퓨터에서 여러 개(시험용 빌드 클라)가 동시에 저장하면 같은 .tmp를 서로 지웠다 (고양이 224)
                string tmp = $"{FilePath}.{System.Diagnostics.Process.GetCurrentProcess().Id}.tmp";
                File.WriteAllText(tmp, json);
                // 쓰는 도중 꺼져도 직전본이 남게: 기존 파일 → .bak, 임시 파일 → 본 파일
                // 본 파일이 깨져 있으면(직전본에서 불러온 경우) 직전본에 덮지 않는다 — 덮으면 둘 다 깨진다 (고양이 268)
                if (File.Exists(FilePath) && TryRead(FilePath, quiet: true) != null) File.Copy(FilePath, BackupPath, true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
                Log.Dev($"저장: 누계 {_data.HaulTotal}");
            }
            catch (Exception e)
            {
                Log.Error($"저장 실패: {e.Message}");
            }
        }

        private static SaveData TryRead(string path, bool quiet = false)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) { if (!quiet) Log.DevWarn($"저장 파일이 비어 있음 ({Path.GetFileName(path)})"); return null; } // 쓰다 꺼진 빈 파일
                return JsonUtility.FromJson<SaveData>(text);
            }
            catch (Exception e)
            {
                if (!quiet) Log.Error($"저장 파일 읽기 실패 ({Path.GetFileName(path)}): {e.Message}");
                return null;
            }
        }

        // 도메인 리로드를 끈 플레이 모드에서도 매 플레이마다 파일에서 새로 읽게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _data = null;
    }
}
