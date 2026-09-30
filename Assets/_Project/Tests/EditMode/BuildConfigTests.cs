using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RatGame.EditorTools;
using UnityEditor;

namespace RatGame.Tests
{
    /// <summary>
    /// 스팀 업로드 설정 (고양이 321, steam/README.md) — 업로드가 빌드 메뉴가 만든 폴더를 가리키는지, 디포 번호가 두 파일에서 같은지.
    /// 어긋나면 빈 폴더나 옛 빌드를 올리게 된다.
    /// </summary>
    public class BuildConfigTests
    {
        private static string Value(string vdf, string key)
        {
            var m = Regex.Match(vdf, $"\"{key}\"\\s+\"([^\"]*)\"");
            Assert.IsTrue(m.Success, $"{key} 없음");
            return m.Groups[1].Value;
        }

        [Test]
        public void 스팀_업로드는_윈도_릴리스_빌드_폴더를_올림()
        {
            string app = File.ReadAllText("steam/app_build_demo.vdf");
            string root = Path.GetFullPath(Path.Combine("steam", Value(app, "ContentRoot")));
            string built = Path.GetFullPath(Path.GetDirectoryName(BuildTools.OutputPath(BuildTarget.StandaloneWindows64, false)));
            Assert.AreEqual(built.TrimEnd('/'), root.TrimEnd('/'));
            Assert.AreEqual("", Value(app, "SetLive"), "업로드하자마자 공개되면 안 된다 — 브랜치 연결은 사람이 웹에서");
        }

        [Test]
        public void 스팀_디포_번호가_두_파일에서_같음()
        {
            string app = File.ReadAllText("steam/app_build_demo.vdf");
            var depots = Regex.Match(app, "\"Depots\"\\s*\\{\\s*\"([^\"]+)\"\\s+\"([^\"]+)\"");
            Assert.IsTrue(depots.Success, "Depots 항목 없음");
            string depotFile = Path.Combine("steam", depots.Groups[2].Value);
            Assert.IsTrue(File.Exists(depotFile), $"디포 설정 파일 없음: {depotFile}");
            Assert.AreEqual(depots.Groups[1].Value, Value(File.ReadAllText(depotFile), "DepotID"));
        }

        [Test]
        public void 스팀_안내의_도전과제_이름이_실제와_같음() // Steamworks에 등록할 API 이름 목록 (고양이 326)
        {
            string readme = File.ReadAllText("steam/README.md");
            var block = Regex.Match(readme, "<!-- achievements:start -->(.*?)<!-- achievements:end -->", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "README에 도전과제 목록 없음");
            var listed = new System.Collections.Generic.SortedSet<string>();
            foreach (Match m in Regex.Matches(block.Groups[1].Value, "`([^`]+)`")) listed.Add(m.Groups[1].Value);
            var actual = new System.Collections.Generic.SortedSet<string>();
            foreach (var a in UnityEngine.Resources.LoadAll<RatGame.Data.AchievementSO>("Achievements")) actual.Add(a.Id);
            CollectionAssert.AreEqual(actual, listed);
        }
    }
}
