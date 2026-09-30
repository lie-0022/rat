using System;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using RatGame.AI;
using RatGame.Core;
using RatGame.Run;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 도전과제 판정 (고양이 222 — 220에서 실제로 못 본 것). 혼자 창고:
    /// ① 고양이에게 일부러 쫓김 → 귀환해도 유령 쥐 통계가 안 오름 ② 계란 3개 정산 → 계란 택배 ③ 혼자 귀환 → 혼자 살아남기.
    /// 세이브는 시작 때 떠 두고 끝나면 파일을 되돌린다 (런을 끝내니까).
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Achievement Checks")]
        private static void ArmAchievements() => Arm("achv");

        private static string _saveBefore, _bakBefore;
        private static Action RestoreAfterPlay; // 플레이가 멈춘 뒤 한 번 더 (고양이 234)

        /// <summary>세이브·백업을 떠 두고, 되돌리는 동작을 돌려준다(시험 끝 OnDone) — 플레이가 멈춘 뒤에도 한 번 더 되돌린다.</summary>
        private static Action BackupSave(string testName)
        {
            string bak = Path.Combine(Application.persistentDataPath, "save.bak");
            string save = File.Exists(SaveService.FilePath) ? File.ReadAllText(SaveService.FilePath) : null;
            string backup = File.Exists(bak) ? File.ReadAllText(bak) : null;
            _saveBefore = save; _bakBefore = backup;
            Action restore = () =>
            {
                if (save != null) File.WriteAllText(SaveService.FilePath, save);
                if (backup != null) File.WriteAllText(bak, backup);
            };
            RestoreAfterPlay = restore;
            return () => { restore(); Debug.Log($"[Rat] {testName} 시험: 세이브 되돌림 (플레이를 멈추면 한 번 더)"); };
        }
        private static bool _sawChase;

        private static int Stat(string key)
        {
            var e = SaveService.Data.Stats.Find(s => s.Key == key);
            return e != null ? e.Value : 0;
        }

        private static List<Step> AchievementSteps()
        {
            int ghostBefore = 0, soloBefore = 0;
            return new List<Step>
            {
                new Step { Name = "세이브 뜨기", Act = () =>
                {
                    OnDone = BackupSave("도전과제 판정");
                    ghostBefore = Stat("ghostClears"); soloBefore = Stat("soloReturns");
                    _sawChase = false;
                    Cat().ServerWake(); // 창고 고양이는 자고 있을 수 있다 — 자면 2m 앞에 서도 안 쫓는다 (첫 판 30초 추격 0)
                    CatBrain.ServerStateChanged -= OnChaseSeen;
                    CatBrain.ServerStateChanged += OnChaseSeen;
                } },
                // ① 쫓기기 — 고양이 앞 2m에 붙여 두다가 추격이 시작되면 바로 쥐구멍 옆으로 빼고 고양이를 멈춘다(잡혀 쓰러지면 귀환이 안 됨)
                new Step { Name = "추격 당하기", Ready = () =>
                {
                    var cat = Cat();
                    if (_sawChase)
                    {
                        cat.enabled = false;
                        var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                        Put(new Vector3(zone.center.x, zone.min.y + 0.6f, zone.min.z - 2f));
                        return true;
                    }
                    if (EditorApplication.timeSinceStartup - _stepAt > 30) return true;
                    // 숨거나 자면 앞에 서도 못 본다 — 계속 깨움 (추격 시험과 같은 규칙, Full에서 30초 추격 0 — 고양이 317)
                    var st = cat.State.Value;
                    if (st == CatState.Ambush || st == CatState.BoxSit || st == CatState.Sleep) cat.ServerWake();
                    var fwd = cat.transform.forward; fwd.y = 0f; fwd.Normalize();
                    Put(cat.transform.position + fwd * 1.5f + Vector3.up * 0.3f);
                    return false;
                }, Check = () => _sawChase ? null : "30초 안에 추격이 안 일어남" },
                // ② 계란 3개를 쥐구멍 칸 안에 스폰 — 떨어지며 정산된다
                new Step { Name = "계란 3개", Wait = 1f, Act = () =>
                {
                    CatBrain.ServerStateChanged -= OnChaseSeen;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/Loot/loot_egg.prefab");
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    for (int i = 0; i < 3; i++)
                    {
                        var go = Object.Instantiate(prefab, zone.center + new Vector3((i - 1) * 0.3f, 0.3f, 0f), Quaternion.identity);
                        go.GetComponent<NetworkObject>().Spawn(true);
                    }
                } },
                new Step { Name = "계란 택배", Wait = 2f, Check = () =>
                {
                    Report.Append($" | 계란 한 판 {Stat("eggsInOneRun")}");
                    return SaveService.Data.CompletedAchievementIds.Contains("ach_egg_courier") ? null : "계란 택배 미달성";
                } },
                // ③ 혼자 귀환
                new Step { Name = "귀환", Act = () =>
                {
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    Put(zone.center + Vector3.up * 0.2f);
                } },
                new Step { Name = "결과", Ready = () => RunManager.Instance != null && RunManager.Instance.IsShowingResult || EditorApplication.timeSinceStartup - _stepAt > 15,
                    Check = () =>
                    {
                        int ghost = Stat("ghostClears") - ghostBefore, solo = Stat("soloReturns") - soloBefore;
                        Report.Append($" | 유령 +{ghost} · 혼자 +{solo}");
                        if (!RunManager.Instance.IsShowingResult) return "귀환 안 됨";
                        if (ghost != 0) return "쫓겼는데 유령 쥐 통계가 오름";
                        if (solo != 1) return "혼자 귀환이 안 셈";
                        return SaveService.Data.CompletedAchievementIds.Contains("ach_survivor") ? null : "혼자 살아남기 미달성";
                    } },
            };
        }

        private static void OnChaseSeen(CatBrain cat, CatState prev, CatState next)
        {
            if (next == CatState.Chase) _sawChase = true;
        }
    }
}
