using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// 문서 표 수치 = BalanceConfig (docs/14 "Validate Balance", 고양이 328) — 규칙 2(새 수치는 docs 표에도)가 어긋나면 잡는다.
    /// 표 첫 칸이 필드 이름 그대로(`walkSpeed`)인 줄만 본다 — 값 칸의 첫 숫자를 SO 값과 비교. 그렇게 적힌 줄이 늘수록 넓게 지킨다.
    /// </summary>
    public class BalanceDocsTests
    {
        [Test]
        public void 문서_표의_수치가_BalanceConfig와_같음()
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Data/Balance/BalanceConfig.asset");
            Assert.NotNull(so);
            var values = new Dictionary<string, double>();
            var it = new SerializedObject(so).GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                string name = it.name.TrimStart('_');
                if (it.propertyType == SerializedPropertyType.Float) values[name] = it.floatValue;
                else if (it.propertyType == SerializedPropertyType.Integer) values[name] = it.intValue;
            }

            var row = new Regex(@"^\|\s*`?([A-Za-z][A-Za-z0-9]*)`?\s*\|\s*([^|]*)\|");
            var number = new Regex(@"-?\d+(\.\d+)?");
            var wrong = new List<string>();
            int checkedRows = 0;
            foreach (var file in Directory.GetFiles("docs", "*.md"))
                foreach (var line in File.ReadAllLines(file))
                {
                    var m = row.Match(line);
                    if (!m.Success || !values.TryGetValue(m.Groups[1].Value, out double actual)) continue;
                    var n = number.Match(m.Groups[2].Value);
                    if (!n.Success) continue;
                    double documented = double.Parse(n.Value, System.Globalization.CultureInfo.InvariantCulture);
                    checkedRows++;
                    if (System.Math.Abs(documented - actual) > 1e-4 * System.Math.Max(1.0, System.Math.Abs(documented)))
                        wrong.Add($"{Path.GetFileName(file)} {m.Groups[1].Value}: 문서 {documented} · SO {actual}");
                }
            Assert.IsEmpty(wrong, "문서와 BalanceConfig가 다름 — 문서를 먼저 고치고 SO를 맞춘다:\n" + string.Join("\n", wrong));
            Assert.Greater(checkedRows, 20, "비교한 줄이 너무 적음 — 표 형식이 바뀌었나");
        }
    }
}
