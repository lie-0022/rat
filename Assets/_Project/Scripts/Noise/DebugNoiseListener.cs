using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Noise
{
    /// <summary>
    /// 임시 큐브 리스너 (docs/06 수용 기준용). CatSenses(태스크 1-5)가 생기면 대체된다.
    /// 호스트에서 OnNoise 구독 → 유효 loudness 로그. 벽 감쇠·반경 검증에 사용.
    /// </summary>
    public class DebugNoiseListener : MonoBehaviour
    {
        private void OnEnable() => NoiseSystem.OnNoise += OnNoise;
        private void OnDisable() => NoiseSystem.OnNoise -= OnNoise;

        private void OnNoise(NoiseEvent e)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            float heard = NoiseSystem.GetLoudnessAt(e, transform.position);
            if (heard <= 0f) return;
            Log.Dev($"[{name}] 소음 감지: {e.Type} 원본 {e.Loudness:F0} → 유효 {heard:F0} " +
                    $"(거리 {Vector3.Distance(e.Pos, transform.position):F1}m)");
        }
    }
}
