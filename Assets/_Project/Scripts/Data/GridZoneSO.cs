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
        public GameObject TreasureGlow;       // 보물방 금빛 표시 (고양이 88)

        [Header("스테이지 깊이별 난이도 (고양이 66, 잠정) — 배열 끝을 넘으면 마지막 값")]
        public int[] CatCountByStage = { 1, 1, 2, 2, 3 };
        public float[] TrapRatioByStage = { 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        public float[] KittenChanceByStage = { 0f, 0f, 0.5f, 0.5f, 0.6f }; // 고양이 2마리 이상일 때 한 마리가 아기일 확률 (엄마·아기, 고양이 75)
        public float KittenChanceFor(int stage) => KittenChanceByStage == null || KittenChanceByStage.Length == 0 ? 0f : KittenChanceByStage[Mathf.Clamp(stage - 1, 0, KittenChanceByStage.Length - 1)];
        public float[] GuardChanceByStage = { 0f, 0f, 1f, 1f, 1f }; // 어른 고양이 한 마리가 목적지 앞 문지기가 될 확률 (고양이 80)
        public float GuardChanceFor(int stage) => GuardChanceByStage == null || GuardChanceByStage.Length == 0 ? 0f : GuardChanceByStage[Mathf.Clamp(stage - 1, 0, GuardChanceByStage.Length - 1)];
        public float[] PatrolChanceByStage = { 0f, 0f, 0f, 0.5f, 1f }; // 문지기·아기가 아닌 어른이 남으면 큰길 순찰꾼이 될 확률 (고양이 92)
        public float PatrolChanceFor(int stage) => PatrolChanceByStage == null || PatrolChanceByStage.Length == 0 ? 0f : PatrolChanceByStage[Mathf.Clamp(stage - 1, 0, PatrolChanceByStage.Length - 1)];
        public float[] ModifierChanceByStage = { 0f, 0.5f, 0.6f, 0.7f, 0.8f }; // 오늘의 집 — 스테이지 조건 확률 (고양이 106)
        public float ModifierChanceFor(int stage) => ModifierChanceByStage == null || ModifierChanceByStage.Length == 0 ? 0f : ModifierChanceByStage[Mathf.Clamp(stage - 1, 0, ModifierChanceByStage.Length - 1)];
        public float BlackoutDarkChance = 0.9f;  // 정전 — 방마다 어둠 구역 확률
        public float TrapSaleBonus = 0.2f;       // 덫 대방출 — 함정 비율 더하기
        public float TreatsCatSpeed = 0.85f;     // 고양이 간식 날 — 고양이 이동 배율
        public int BusyExtraEvents = 2;          // 분주한 집 — 집주인 사건 더하기
        public bool TreasureRoom = true;        // 가장 먼 막다른 방 = 보물방 (전리품 전부·대형 ×3·함정 전부, 고양이 84)
        public float TreasureTrapRatio = 1f;
        public float TreasureBigWeight = 8f;    // 보물방 대형·특수 가중 (v1 보너스방 3보다 세게 — 작은 방도 비싸 보이게)

        public int CatCountFor(int stage) => CatCountByStage == null || CatCountByStage.Length == 0 ? -1 : CatCountByStage[Mathf.Clamp(stage - 1, 0, CatCountByStage.Length - 1)];
        public float TrapRatioFor(int stage) => TrapRatioByStage == null || TrapRatioByStage.Length == 0 ? -1f : TrapRatioByStage[Mathf.Clamp(stage - 1, 0, TrapRatioByStage.Length - 1)];
    }
}
