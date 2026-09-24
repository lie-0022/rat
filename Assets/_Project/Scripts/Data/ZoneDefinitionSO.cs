using UnityEngine;

namespace RatGame.Data
{
    /// <summary>존 정의 (docs/10, 2026-09-24 고양이 57). 방 개수는 BalanceConfig roomModulesPerZone.</summary>
    [CreateAssetMenu(fileName = "Zone", menuName = "RatGame/Zone Definition")]
    public class ZoneDefinitionSO : ScriptableObject
    {
        public string ThemeId = "kitchen";
        public GameObject RatHoleRoom;          // 시작방 (쥐구멍 포함) 프리팹
        public GameObject[] MiddleRoomPool;     // 테마당 4~6개
        public GameObject[] BonusRoomPool;      // 1~2개 (고가치·고위험)
        [Range(0f, 1f)] public float BonusRoomChance = 0.4f;
        public int CatCount = 1;
        public SpawnTableSO LootTable;
        public TrapTableSO TrapTable;
        public GameObject RatHolePrefab;        // 쥐구멍(DepositZone) — 쥐구멍방 RatHole 표시에
        public GameObject CatPrefab;
        public GameObject[] HideSpotPrefabs;    // 숨을 곳 종류 (고양이 60) — HideSpawns마다 시드로 하나
        public GameObject DarkZonePrefab;       // 어둠 구역 (LightZone)
        [Range(0f, 1f)] public float DarkZoneChance = 0.6f; // DarkZone 표시가 있는 방이 실제로 어두울 확률
    }
}
