using UnityEngine;

namespace RatGame.Data
{
    /// <summary>
    /// 생성기 v2 테마 (docs/10 "벽 속", 2026-09-24 고양이 62). 칸 그래프 수치 + 역할별 방 풀 + 통로.
    /// 전리품·함정·숨을 곳·어둠·고양이 테이블은 v1 존 정의(Population)를 그대로 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "GridZone", menuName = "RatGame/Grid Zone")]
    public class GridZoneSO : ScriptableObject
    {
        public string ThemeId = "walls";
        public float CellSize = 14f;
        public int MainPathMin = 5, MainPathMax = 7;
        [Range(0f, 1f)] public float BranchChance = 0.6f;
        public int BranchMaxDepth = 2;
        public int LoopMinGap = 3;

        public GameObject StartRoom;          // 출발방 (벽 속 입구, PlayerSpawn)
        public GameObject DestinationRoom;    // 목적지방 (식량 창고·상점)
        public GameObject[] MainRoomPool;
        public GameObject[] BranchRoomPool;
        public GameObject[] DeadEndRoomPool;
        public GameObject Corridor;           // 보통 연결
        public GameObject Pipe;               // 고리를 닫는 연결 (벽 속 파이프)
        public ZoneDefinitionSO Population;
        public StageShopSO Shop;              // 목적지방 상점 (새 루프, 고양이 65)
        public GameObject ShopCounterPrefab;  // 목적지방 판매대

        [Header("스테이지 깊이별 난이도 (고양이 66, 잠정) — 배열 끝을 넘으면 마지막 값")]
        public int[] CatCountByStage = { 1, 1, 2, 2, 3 };
        public float[] TrapRatioByStage = { 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        public float[] KittenChanceByStage = { 0f, 0f, 0.5f, 0.5f, 0.6f }; // 고양이 2마리 이상일 때 한 마리가 아기일 확률 (엄마·아기, 고양이 75)
        public float KittenChanceFor(int stage) => KittenChanceByStage == null || KittenChanceByStage.Length == 0 ? 0f : KittenChanceByStage[Mathf.Clamp(stage - 1, 0, KittenChanceByStage.Length - 1)];
        public float[] GuardChanceByStage = { 0f, 0f, 1f, 1f, 1f }; // 어른 고양이 한 마리가 목적지 앞 문지기가 될 확률 (고양이 80)
        public float GuardChanceFor(int stage) => GuardChanceByStage == null || GuardChanceByStage.Length == 0 ? 0f : GuardChanceByStage[Mathf.Clamp(stage - 1, 0, GuardChanceByStage.Length - 1)];

        public int CatCountFor(int stage) => CatCountByStage == null || CatCountByStage.Length == 0 ? -1 : CatCountByStage[Mathf.Clamp(stage - 1, 0, CatCountByStage.Length - 1)];
        public float TrapRatioFor(int stage) => TrapRatioByStage == null || TrapRatioByStage.Length == 0 ? -1f : TrapRatioByStage[Mathf.Clamp(stage - 1, 0, TrapRatioByStage.Length - 1)];
    }
}
