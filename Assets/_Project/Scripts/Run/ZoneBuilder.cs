using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using RatGame.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RatGame.Run
{
    /// <summary>
    /// 생성 스테이지 (docs/10, 2026-09-24 고양이 58). 호스트가 스테이지 씬이 스폰될 때 존을 만들고 전부 NetworkObject로 스폰 —
    /// 클라는 스폰을 받기만 한다(같은 시드 보장 불필요). 순서: 방 → NavMesh 굽기 → 쥐구멍 → 전리품(테이블 70%) → 함정(50%) → 고양이 → 쥐 배치.
    /// 방 프리팹은 정적 지오메트리만(중첩 NetworkObject 없음), 움직이는 건 전부 따로 스폰.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class ZoneBuilder : NetworkBehaviour
    {
        [SerializeField] private ZoneDefinitionSO _zone;
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private int _fixedSeed; // 0이면 무작위 (테스트용 고정)

        public ZoneLayout Layout { get; private set; }
        public int Seed { get; private set; }
        public int LootSpawned { get; private set; }
        public int LootValue { get; private set; }
        public int TrapsSpawned { get; private set; }
        public int CatsSpawned { get; private set; }
        public int HidesSpawned { get; private set; }
        public int DarkSpawned { get; private set; }
        public ZonePopulator Populator { get; private set; }
        public Vector3? RingPos => Populator?.RingPos;
        public Vector3 RingAnchor => Populator != null ? Populator.RingAnchor : default;

        public override void OnNetworkSpawn()
        {
            if (!IsServer || _zone == null) return;
            // 기지에서 출발해 씬을 로드하는 중이면 클라가 아직 로딩 중 — 이때 스폰한 오브젝트는 클라에 안 간다
            // (NGO가 버림: 방·전리품 없이 허공에 섬, 고양이 59). 전원 로드 완료 뒤에 만든다. 씬 직접 플레이는 바로.
            if (RunSession.DepartPending) NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            else BuildNow();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }

        private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != gameObject.scene.name) return;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            BuildNow();
        }

        private void BuildNow() => Build(_fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue), 0);

        private void Build(int seed, int zoneIndex)
        {
            Seed = seed;
            var rng = new System.Random(seed ^ 0x5eed);
            Layout = new ZoneGenerator().Generate(_zone, _balance, seed, zoneIndex, null);
            var rooms = new List<RoomModule>(Layout.Rooms);
            if (Layout.BonusRoom != null) rooms.Add(Layout.BonusRoom);
            foreach (var r in rooms) r.GetComponent<NetworkObject>().Spawn(true);

            // NavMesh — 방 지오메트리만 (바닥·벽·상자 레이어). 움직이는 물건·쥐·고양이는 넣지 않는다
            Physics.SyncTransforms();
            var surface = GetComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.layerMask = LayerMask.GetMask("RoomStatic", "NoiseBlocker");
            surface.BuildNavMesh();

            var first = Layout.Rooms[0];
            if (_zone.RatHolePrefab != null && first.RatHole != null)
                ZonePopulator.Spawn(_zone.RatHolePrefab, first.RatHole.position + Vector3.up * 0.15f, first.RatHole.rotation);

            Populator = new ZonePopulator(_zone, rooms, Layout.Rooms[0], Layout.BonusRoom);
            Populator.PopulateAll(rng, zoneIndex);
            LootSpawned = Populator.LootSpawned; LootValue = Populator.LootValue; TrapsSpawned = Populator.TrapsSpawned;
            CatsSpawned = Populator.CatsSpawned; HidesSpawned = Populator.HidesSpawned; DarkSpawned = Populator.DarkSpawned;
            PlayerPlacement.TeleportAllToSpawns(); // 직접 플레이(기지를 안 거침)에서도 쥐구멍방에 서게
            Log.Dev($"존 스폰: 시드 {seed} — 방 {rooms.Count}, 전리품 {LootSpawned}개(가치 {LootValue}), 함정 {TrapsSpawned}, 숨을 곳 {HidesSpawned}, 어둠 {DarkSpawned}, 고양이 {CatsSpawned}");
        }


#if UNITY_EDITOR
        public void EditorSetup(ZoneDefinitionSO zone, BalanceConfigSO balance) { _zone = zone; _balance = balance; }
#endif
    }
}
