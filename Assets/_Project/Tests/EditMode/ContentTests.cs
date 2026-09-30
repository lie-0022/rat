using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RatGame.Data;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 데이터 무결성 (고양이 291) — 번역 표·도전과제·아이템 데이터베이스. 에셋을 고치다 빠뜨린 것을 바로 잡는다.
    /// </summary>
    public class ContentTests
    {
        private static readonly Regex Slot = new(@"\{(\d+)(?::[^}]*)?\}");

        private static HashSet<string> Slots(string s)
        {
            var set = new HashSet<string>();
            if (s != null) foreach (Match m in Slot.Matches(s)) set.Add(m.Groups[1].Value);
            return set;
        }

        [Test]
        public void 번역_표_영어_빈칸_없고_자리_일치()
        {
            var table = Resources.Load<LocalizationTableSO>("LocalizationTable");
            Assert.NotNull(table);
            var keys = new HashSet<string>();
            foreach (var e in table.Entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.En), $"영어 빈 칸: {e.Ko}");
                Assert.IsTrue(keys.Add(e.Ko), $"같은 원문 두 번: {e.Ko}");
                Assert.IsTrue(Slots(e.Ko).SetEquals(Slots(e.En)), $"{{n}} 자리 불일치: \"{e.Ko}\" → \"{e.En}\"");
            }
            Assert.Greater(keys.Count, 400);
        }

        [Test]
        public void 도전과제_아이디_겹침_없고_목표_양수()
        {
            var ids = new HashSet<string>();
            var all = Resources.LoadAll<AchievementSO>("Achievements");
            Assert.Greater(all.Length, 0);
            foreach (var a in all)
            {
                Assert.IsFalse(string.IsNullOrEmpty(a.Id), a.name);
                Assert.IsTrue(ids.Add(a.Id), $"도전과제 아이디 겹침: {a.Id}");
                Assert.Greater(a.Target, 0, a.Id);
                Assert.IsFalse(string.IsNullOrEmpty(a.StatKey), $"{a.Id} 통계 키 없음");
                Assert.IsFalse(string.IsNullOrEmpty(a.Title), $"{a.Id} 제목 없음");
            }
        }

        [Test]
        public void 빌드_씬_Boot가_0번이고_모두_있음()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.Greater(scenes.Length, 0);
            Assert.IsTrue(scenes[0].path.EndsWith("/Boot.unity"), $"0번이 Boot가 아님: {scenes[0].path}"); // CLAUDE.md 빌드 규칙
            foreach (var sc in scenes)
                if (sc.enabled) Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(sc.path), $"빌드 씬 파일 없음: {sc.path}");
        }

        [Test]
        public void 네트워크_프리팹은_모두_목록에() // 빠지면 스폰 때 클라 오류 (고양이 293)
        {
            var nm = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/NetworkManager.prefab").GetComponent<Unity.Netcode.NetworkManager>();
            var registered = new HashSet<GameObject>();
            foreach (var list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                if (list != null) foreach (var p in list.PrefabList) if (p.Prefab != null) registered.Add(p.Prefab);
            foreach (var p in nm.NetworkConfig.Prefabs.Prefabs) if (p.Prefab != null) registered.Add(p.Prefab);
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go.GetComponent<Unity.Netcode.NetworkObject>() == null) continue;
                count++;
                Assert.IsTrue(registered.Contains(go), $"네트워크 프리팹 목록에 없음: {path}");
            }
            Assert.Greater(count, 50);
        }

        [Test]
        public void 프리팹에_사라진_스크립트_없음() // 스크립트를 지우거나 옮기다 남은 빈 부품 (고양이 294)
        {
            int prefabs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                prefabs++;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), $"사라진 스크립트: {path} / {t.name}");
            }
            Assert.Greater(prefabs, 50);
        }

        [Test]
        public void 프리팹_번역_글자는_모두_표에() // 없으면 영어 모드에서 그 글자만 한국어로 남는다 (고양이 303)
        {
            var table = Resources.Load<LocalizationTableSO>("LocalizationTable");
            int checkedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var lt in go.GetComponentsInChildren<UI.LocalizedText>(true))
                {
                    if (string.IsNullOrEmpty(lt.Ko)) continue;
                    checkedCount++;
                    Assert.IsTrue(table.TryGetEn(lt.Ko, out _), $"번역 표에 없음: {path} / {lt.name} \"{lt.Ko}\"");
                }
            }
            Assert.Greater(checkedCount, 50);
        }

        [Test]
        public void 코드_번역_원문은_모두_표에() // Loc.T/F/Pack("…") 한국어 — 번역 도구 검사와 같은 규칙 (고양이 304)
        {
            var table = Resources.Load<LocalizationTableSO>("LocalizationTable");
            // 접속 거절·끊김 사유도 — 호스트가 한국어 원문을 보내고 클라 메뉴가 Loc.T로 번역한다 (고양이 318)
            var call = new Regex(@"(?:Loc\.(?:T|F|Pack)\(|\.Reason = |DisconnectClient\()([^;\n]*)");
            var lit = new Regex(@"""((?:[^""\\]|\\.)*)""");
            var hangul = new Regex("[가-힣]");
            var missing = new List<string>();
            foreach (var file in System.IO.Directory.GetFiles("Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Editor/")) continue;
                foreach (Match c in call.Matches(System.IO.File.ReadAllText(file)))
                    foreach (Match l in lit.Matches(c.Groups[1].Value))
                    {
                        string ko = Regex.Unescape(l.Groups[1].Value);
                        if (hangul.IsMatch(ko) && !table.TryGetEn(ko, out _)) missing.Add($"{System.IO.Path.GetFileName(file)}: {ko}");
                    }
            }
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void UI_코드의_한국어_글자도_모두_표에() // switch로 고른 글자·.text에 바로 넣는 글자 — Loc 검사에 안 잡혀 의심 이유가 한국어로 남았다 (고양이 305)
        {
            var table = Resources.Load<LocalizationTableSO>("LocalizationTable");
            var arm = new Regex(@"(?:=>|\.text\s*=)\s*""((?:[^""\\]|\\.)*)""");
            var hangul = new Regex("[가-힣]");
            var missing = new List<string>();
            foreach (var file in System.IO.Directory.GetFiles("Assets/_Project/Scripts/UI", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string name = System.IO.Path.GetFileName(file);
                if (name.StartsWith("DevCheckMenu") || name.StartsWith("CatDebugOverlay")) continue; // 개발용 화면
                foreach (Match m in arm.Matches(System.IO.File.ReadAllText(file)))
                {
                    string ko = Regex.Unescape(m.Groups[1].Value);
                    if (hangul.IsMatch(ko) && !table.TryGetEn(ko, out _)) missing.Add($"{name}: {ko}");
                }
            }
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void 세이브_JSON_왕복() // 파일은 안 쓴다 — 메모리에서만
        {
            var d = new Core.SaveData { HaulTotal = 1234, BestStage = 5, Endings = 2, BodyColorHex = "#59CCD9" };
            d.UnlockedCodexIds.Add("loot_egg");
            d.Stats.Add(new Core.StatEntry { Key = "deepestStage", Value = 5 });
            d.CompletedAchievementIds.Add("ach_deep_rat");
            var back = JsonUtility.FromJson<Core.SaveData>(JsonUtility.ToJson(d));
            Assert.AreEqual(1234, back.HaulTotal);
            Assert.AreEqual(5, back.BestStage);
            Assert.AreEqual("#59CCD9", back.BodyColorHex);
            Assert.AreEqual("loot_egg", back.UnlockedCodexIds[0]);
            Assert.AreEqual("deepestStage", back.Stats[0].Key);
            Assert.AreEqual(5, back.Stats[0].Value);
            Assert.AreEqual("ach_deep_rat", back.CompletedAchievementIds[0]);
            var empty = JsonUtility.FromJson<Core.SaveData>("{}"); // 옛 세이브(필드 없음)도 목록이 비어 있지 null이 아님
            Assert.NotNull(empty.Stats);
            Assert.NotNull(empty.CompletedAchievementIds);
        }

        [Test]
        public void 아이템_아이디_해시_겹침_없고_전리품은_값과_프리팹()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ItemDatabase")[0]));
            Assert.NotNull(db);
            var hashes = new Dictionary<int, string>();
            foreach (var item in db.Items)
            {
                Assert.NotNull(item, "데이터베이스에 빈 칸");
                Assert.IsFalse(string.IsNullOrEmpty(item.Id), item.name);
                int h = ItemDatabase.HashId(item.Id);
                Assert.IsFalse(hashes.ContainsKey(h), $"아이디 해시 겹침: {item.Id} / {(hashes.TryGetValue(h, out var o) ? o : "")}");
                hashes[h] = item.Id;
                Assert.AreSame(item, db.GetById(item.Id), item.Id);
                if (!item.Id.StartsWith("loot_")) continue;
                Assert.Greater(item.BaseValue, 0, $"{item.Id} 값 0");
                Assert.Greater(item.Mass, 0f, $"{item.Id} 무게 0");
                Assert.NotNull(item.Prefab, $"{item.Id} 프리팹 없음");
            }
        }
    }
}
