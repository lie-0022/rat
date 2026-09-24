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
    }
}
