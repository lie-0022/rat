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
