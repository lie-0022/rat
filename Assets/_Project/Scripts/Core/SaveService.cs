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
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                // 쓰는 도중 꺼져도 직전본이 남게: 기존 파일 → .bak, 임시 파일 → 본 파일
                if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
                Log.Dev($"저장: 누계 {_data.HaulTotal}");
            }
            catch (Exception e)
            {
                Log.Error($"저장 실패: {e.Message}");
            }
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Log.Error($"저장 파일 읽기 실패 ({Path.GetFileName(path)}): {e.Message}");
                return null;
            }
        }

        // 도메인 리로드를 끈 플레이 모드에서도 매 플레이마다 파일에서 새로 읽게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _data = null;
    }
}
