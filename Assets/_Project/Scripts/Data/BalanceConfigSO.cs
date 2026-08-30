using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 밸런스 수치 단일 SO (docs/02). 하드코딩 금지 — 새 수치는 여기 추가하고 해당 docs 표와 일치시킨다.
    /// 초기값 출처: docs/00-overview.md 핵심 수치 표.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "RatGame/Balance Config")]
    public class BalanceConfigSO : ScriptableObject
    {
        [Header("런 구성 (docs/00)")]
        [SerializeField] private int _maxPlayers = 4;
        [SerializeField] private int _maxZonesPerRun = 5;
        [SerializeField] private Vector2Int _roomModulesPerZone = new(3, 4);

        [Header("할당량 곡선 — Zone n 할당량 = base × growth^(n-1)")]
        [SerializeField] private int _quotaBase = 100;
        [SerializeField] private float _quotaGrowth = 1.6f;

        [Header("솔로 보정")]
        [SerializeField, Range(0f, 1f)] private float _soloQuotaMultiplier = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _soloCatVisionMultiplier = 0.8f; // 시야 -20%

        public int MaxPlayers => _maxPlayers;
        public int MaxZonesPerRun => _maxZonesPerRun;
        public Vector2Int RoomModulesPerZone => _roomModulesPerZone;
        public float SoloQuotaMultiplier => _soloQuotaMultiplier;
        public float SoloCatVisionMultiplier => _soloCatVisionMultiplier;

        /// <summary>Zone n(1부터)의 할당량. 솔로면 solo 배수 적용 (docs/00 표).</summary>
        public int GetQuota(int zoneNumber, int playerCount)
        {
            float quota = _quotaBase * Mathf.Pow(_quotaGrowth, zoneNumber - 1);
            if (playerCount <= 1) quota *= _soloQuotaMultiplier;
            return Mathf.RoundToInt(quota);
        }

        /// <summary>구역별 고양이 수: Zone1: 1, Zone3+: 2 (docs/00 표).</summary>
        public int GetCatCount(int zoneNumber) => zoneNumber >= 3 ? 2 : 1;
    }
}
