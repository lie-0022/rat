using System.Collections.Generic;
using System.Reflection;
using RatGame.AI;
using UnityEditor;
using UnityEngine;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 관심 점수 (고양이 261·266, design/cat-ideas/13). 혼자 창고: 고양이에게 유인 API를 차례로 — 캣닢 중 레이저·털실 무시,
    /// 털실 4번째는 지루함(시간 절반), 털실 중 레이저가 갈아탐, 레이저 중 먹이 무시. 규칙이 바뀌면 여기서 먼저 잡힌다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Lure Attention Rules")]
        private static void ArmAttention() => Arm("attention");

        private static float DistractLeft(CatBrain cat) =>
            (float)typeof(CatBrain).GetField("_distractUntil", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cat) - Time.time;
        private static float AttentionNow(CatBrain cat) =>
            (float)typeof(CatBrain).GetField("_attentionScore", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cat);

        private static List<Step> AttentionSteps() => new()
        {
            new Step { Name = "고양이 깨우기", Wait = 1f, Act = () => Cat().ServerWake() },
            new Step { Name = "캣닢이 이김", Wait = 1f, Check = () =>
            {
                var cat = Cat();
                Vector3 p = cat.transform.position + cat.transform.forward * 2f;
                cat.ServerDistract(p, 15f, AttentionKind.Catnip, true);
                if (cat.State.Value != CatState.Distracted) return $"캣닢에 안 끌림 ({cat.State.Value})";
                cat.ServerLaserDot(p + Vector3.right * 3f, 5f);
                cat.ServerDistract(p + Vector3.left * 2f, 8f, AttentionKind.Yarn);
                Report.Append($" | 캣닢 뒤 레이저·털실 → 점수 {AttentionNow(cat)}");
                return AttentionNow(cat) == 90f ? null : "캣닢 중에 약한 자극으로 갈아탐";
            } },
            new Step { Name = "지루함", Check = () =>
            {
                var cat = Cat();
                Vector3 p = cat.transform.position + cat.transform.forward * 2f;
                typeof(CatBrain).GetField("_distractUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cat, Time.time - 0.1f); // 캣닢 끝난 셈
                var left = new List<string>();
                for (int i = 0; i < 4; i++) { cat.ServerDistract(p, 8f, AttentionKind.Yarn); left.Add(DistractLeft(cat).ToString("0.0")); }
                Report.Append($" | 털실 4번 {string.Join("/", left)}초");
                return DistractLeft(cat) < 5f ? null : "4번째에 지루함이 안 걸림";
            } },
            new Step { Name = "센 것이 갈아탐", Check = () =>
            {
                var cat = Cat();
                Vector3 p = cat.transform.position + cat.transform.forward * 2f;
                cat.ServerLaserDot(p + Vector3.right * 3f, 5f);
                float afterLaser = AttentionNow(cat);
                cat.ServerDistract(p, 4f, AttentionKind.Bribe);
                Report.Append($" | 털실 → 레이저 {afterLaser} → 먹이 뒤 {AttentionNow(cat)}");
                if (afterLaser != 80f) return "털실 중 레이저로 안 갈아탐";
                return AttentionNow(cat) == 80f ? null : "레이저 중 먹이로 갈아탐";
            } },
        };
    }
}
