using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// BalanceConfig 수치 전체를 docs/02 끝 표로 쓴다 (규칙 2 "새 수치는 docs 표에도", 고양이 334).
    /// 필드 이름·지금 값·코드 주석을 [Header] 묶음별로 — 수치를 바꾸거나 필드를 더하면 다시 돌린다(EditMode BalanceDocsTests가 어긋나면 잡는다).
    /// 표 안은 손으로 고치지 않는다. 설계 설명은 각 시스템 문서가 맡는다.
    /// </summary>
    public static class BalanceDocsWriter
    {
        private const string DocPath = "docs/02-architecture.md";
        private const string AssetPath = "Assets/_Project/Data/Balance/BalanceConfig.asset";
        private const string Start = "<!-- balance:start -->";
        private const string End = "<!-- balance:end -->";
        private static readonly string[] Sources =
        {
            "Assets/_Project/Scripts/Data/BalanceConfigSO.cs",
            "Assets/_Project/Scripts/Data/BalanceConfigSO.CatFields.cs",
        };

        private static readonly Regex HeaderRx = new(@"\[Header\(""([^""]*)""\)\]");
        private static readonly Regex FieldRx = new(@"\[SerializeField[^\]]*\][^;]*?private\s+[\w<>.\[\]]+\s+_(\w+)\s*(=[^;]*)?;\s*(//\s*(.*))?$");

        [MenuItem("Tools/RatGame/Docs/Write Balance Table")]
        public static void Write()
        {
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetPath));
            var sb = new StringBuilder();
            sb.AppendLine(Start);
            sb.AppendLine("<!-- 자동 생성 — Tools/RatGame/Docs/Write Balance Table. 손으로 고치지 말 것 -->");
            int rows = 0;
            foreach (var src in Sources)
            {
                string header = null;
                bool open = false;
                foreach (var raw in File.ReadAllLines(src))
                {
                    var h = HeaderRx.Match(raw);
                    if (h.Success) { header = h.Groups[1].Value; open = false; }
                    var f = FieldRx.Match(raw.Trim());
                    if (!f.Success) continue;
                    var prop = so.FindProperty("_" + f.Groups[1].Value);
                    if (prop == null) continue;
                    if (!open)
                    {
                        sb.AppendLine().AppendLine($"#### {header ?? "기타"}").AppendLine().AppendLine("| 필드 | 값 | 메모 |").AppendLine("|---|---|---|");
                        open = true;
                    }
                    string note = f.Groups[4].Success ? f.Groups[4].Value.Trim().Replace("|", "/") : "";
                    sb.AppendLine($"| {f.Groups[1].Value} | {Format(prop)} | {note} |");
                    rows++;
                }
            }
            sb.Append(End);

            string doc = File.ReadAllText(DocPath);
            int s = doc.IndexOf(Start, System.StringComparison.Ordinal), e = doc.IndexOf(End, System.StringComparison.Ordinal);
            if (s >= 0 && e > s) doc = doc.Substring(0, s) + sb + doc.Substring(e + End.Length);
            else doc = doc.TrimEnd() + "\n\n### 수치 전체 (자동 생성, 고양이 334)\n\n지금 BalanceConfig 값. 바꾼 뒤 메뉴로 다시 쓴다 — 설명·근거는 각 시스템 문서 표에.\n\n" + sb + "\n";
            File.WriteAllText(DocPath, doc);
            Debug.Log($"[Rat] 수치 표 씀: {rows}줄 → {DocPath}");
        }

        private static string Format(SerializedProperty p)
        {
            if (p.isArray && p.propertyType == SerializedPropertyType.Generic)
            {
                var parts = new List<string>();
                for (int i = 0; i < p.arraySize; i++) parts.Add(FormatOne(p.GetArrayElementAtIndex(i)));
                return string.Join(" · ", parts);
            }
            return FormatOne(p);
        }

        private static string FormatOne(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Float => p.floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            SerializedPropertyType.Integer => p.intValue.ToString(CultureInfo.InvariantCulture),
            SerializedPropertyType.Boolean => p.boolValue ? "켬" : "끔",
            SerializedPropertyType.Vector2Int => $"({p.vector2IntValue.x}, {p.vector2IntValue.y})",
            SerializedPropertyType.Vector2 => $"({p.vector2Value.x.ToString("0.###", CultureInfo.InvariantCulture)}, {p.vector2Value.y.ToString("0.###", CultureInfo.InvariantCulture)})",
            SerializedPropertyType.Enum => p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString(),
            _ => p.propertyType.ToString(),
        };
    }
}
