using RatGame.Core;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 오늘의 집 분위기 (고양이 107) — "정전"이면 해와 주변광을 어둡게. 표시만(어둠 구역 판정은 LightZone 그대로).
    /// 스테이지 안내(EventBus.StageBriefing)는 전원에게 오므로 동기화 추가 없음. 씬이 바뀌면 새 씬의 조명으로 돌아간다.
    /// 씬·프리팹을 안 건드리게 스스로 생긴다.
    /// </summary>
    public class StageMoodView : MonoBehaviour
    {
        private const float BlackoutSun = 0.35f;     // 해 세기 배율
        private const float BlackoutAmbient = 0.45f; // 주변광 배율
        private const float BlackoutSky = 0.3f;      // 하늘 밝기(노출) 배율 — 방 위가 뚫려 있어 하늘이 그대로 밝으면 정전 같지 않아서

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("StageMoodView");
            DontDestroyOnLoad(go);
            go.AddComponent<StageMoodView>();
        }

        private void OnEnable() => EventBus.StageBriefing += OnBriefing;
        private void OnDisable() => EventBus.StageBriefing -= OnBriefing;

        private void OnBriefing(int stage, int stages, int quota, byte flags, byte modifier)
        {
            if ((Run.StageModifier)modifier != Run.StageModifier.Blackout) return;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.intensity *= BlackoutSun;
            RenderSettings.ambientIntensity *= BlackoutAmbient;
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                sky = new Material(sky); // 에셋 머티리얼을 바꾸면 에디터에서 그대로 남는다 — 복사본만
                sky.SetFloat("_Exposure", sky.GetFloat("_Exposure") * BlackoutSky);
                RenderSettings.skybox = sky;
            }
            Log.Dev("정전 연출: 해·주변광 어둡게"); // 2인 검증용
        }
    }
}
