using System.Text;
using UnityEditor;
using UnityEngine;

namespace RatGame.Editor
{
    /// <summary>
    /// docs/01 수용 기준 자동 점검 — 태그·레이어·빌드 씬·물리 설정. 결과는 콘솔 한 방 출력.
    /// docs/14의 "반복 세팅은 에디터 툴로" 원칙의 첫 툴.
    /// </summary>
    public static class SetupValidator
    {
        private static readonly string[] RequiredTags = { "RatHole", "LootSpawn", "CatSpawn", "PlayerSpawn", "DoorSocket" };
        private static readonly string[] RequiredLayers = { "Player", "Carryable", "Cat", "Trap", "RoomStatic", "InteractTrigger", "NoiseBlocker", "Ragdoll" };
        private static readonly string[] RequiredScenes = { "Boot", "MainMenu", "Hub", "Run_Kitchen", "Run_Basement", "Sandbox_Net" };

        [MenuItem("Tools/RatGame/Validate Setup")]
        public static void Validate()
        {
            var sb = new StringBuilder("[RatGame] 셋업 점검 (docs/01)\n");
            bool ok = true;

            foreach (var tag in RequiredTags)
            {
                bool has = System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, tag) >= 0;
                ok &= has;
                sb.AppendLine($"  태그 {tag}: {(has ? "OK" : "누락!")}");
            }
            foreach (var layer in RequiredLayers)
            {
                bool has = LayerMask.NameToLayer(layer) >= 0;
                ok &= has;
                sb.AppendLine($"  레이어 {layer}: {(has ? "OK" : "누락!")}");
            }

            var buildScenes = EditorBuildSettings.scenes;
            bool bootFirst = buildScenes.Length > 0 && buildScenes[0].path.EndsWith("Boot.unity");
            ok &= bootFirst;
            sb.AppendLine($"  Boot 빌드 인덱스 0: {(bootFirst ? "OK" : "아님!")}");
            foreach (var scene in RequiredScenes)
            {
                bool has = System.Array.Exists(buildScenes, s => s.path.EndsWith($"/{scene}.unity"));
                ok &= has;
                sb.AppendLine($"  빌드 씬 {scene}: {(has ? "OK" : "누락!")}");
            }

            bool timestep = Mathf.Approximately(Time.fixedDeltaTime, 0.02f);
            ok &= timestep;
            sb.AppendLine($"  Fixed Timestep 0.02: {(timestep ? "OK" : $"아님 ({Time.fixedDeltaTime})!")}");

            bool ragdollOff = Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Ragdoll"));
            ok &= ragdollOff;
            sb.AppendLine($"  충돌 매트릭스 Player↔Ragdoll off: {(ragdollOff ? "OK" : "아님!")}");

            sb.AppendLine(ok ? "=> 전부 통과" : "=> 실패 항목 있음");
            if (ok) Debug.Log(sb.ToString());
            else Debug.LogError(sb.ToString());
        }
    }
}
