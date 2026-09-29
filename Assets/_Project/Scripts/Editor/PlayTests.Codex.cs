using System.Collections.Generic;
using System.IO;
using RatGame.Core;
using RatGame.Data;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 전리품 전부 정산 (고양이 233). 혼자 창고: 전리품 21종(ItemDatabase의 "loot_")을 0.4초마다 하나씩 쥐구멍 칸에 스폰해 떨군다.
    /// ① 모든 전리품 프리팹이 오류 없이 정산되고 도감이 풀리는지 ② 도감 15종째 "수집가". 세이브는 떠 두고 되돌린다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Codex Deposit All")]
        private static void ArmCodexAll() => Arm("codexall");

        private static List<Step> CodexAllSteps()
        {
            var steps = new List<Step>
            {
                new Step { Name = "세이브 뜨기", Act = () =>
                {
                    string bak = Path.Combine(Application.persistentDataPath, "save.bak");
                    _saveBefore = File.Exists(SaveService.FilePath) ? File.ReadAllText(SaveService.FilePath) : null;
                    _bakBefore = File.Exists(bak) ? File.ReadAllText(bak) : null;
                    OnDone = () =>
                    {
                        if (_saveBefore != null) File.WriteAllText(SaveService.FilePath, _saveBefore);
                        if (_bakBefore != null) File.WriteAllText(bak, _bakBefore);
                        Debug.Log("[Rat] 전리품 전부 정산 시험: 세이브 되돌림 (플레이를 멈출 것)");
                    };
                    Report.Append($" | 시작 도감 {SaveService.Data.UnlockedCodexIds.Count}종");
                } },
            };
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ItemDatabase")[0]));
            foreach (var item in db.Items)
            {
                if (item == null || !item.Id.StartsWith("loot_") || item.Prefab == null) continue;
                var it = item;
                steps.Add(new Step { Name = it.Id, Wait = 0.4f, Act = () =>
                {
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    var go = Object.Instantiate(it.Prefab, zone.center + Vector3.up * 0.5f, Quaternion.identity);
                    go.GetComponent<NetworkObject>().Spawn(true);
                } });
            }
            steps.Add(new Step { Name = "도감", Wait = 1.5f, Check = () =>
            {
                var d = SaveService.Data;
                int total = 0, have = 0;
                var missing = new List<string>();
                foreach (var item in db.Items)
                {
                    if (item == null || !item.Id.StartsWith("loot_")) continue;
                    total++;
                    if (d.UnlockedCodexIds.Contains(item.Id)) have++; else missing.Add(item.Id);
                }
                Report.Append($" | 도감 {have}/{total}{(missing.Count > 0 ? " 빠짐 " + string.Join(",", missing) : "")}");
                return have == total ? null : "안 풀린 도감 있음";
            } });
            steps.Add(new Step { Name = "수집가", Check = () =>
                SaveService.Data.CompletedAchievementIds.Contains("ach_collector") ? null : "수집가 미달성" });
            return steps;
        }
    }
}
