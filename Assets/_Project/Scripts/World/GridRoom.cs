using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 생성기 v2 격자 방 (docs/10 "벽 속", 2026-09-24 고양이 62). 네 면 가운데 문틈 + 면마다 막음벽.
    /// 이어진 면만 막음벽을 끈다 — `OpenSides` NV라 클라도 스폰 때 같은 모양(호스트가 스폰 전에 정한다).
    /// 스폰 표시(전리품·함정·숨을 곳·어둠·고양이·쥐 시작)는 같은 오브젝트의 RoomModule이 가진다 — v1 스폰 로직을 그대로 쓰려고.
    /// </summary>
    [RequireComponent(typeof(RoomModule))]
    public class GridRoom : NetworkBehaviour
    {
        public const byte North = 1, East = 2, South = 4, West = 8;

        [SerializeField] private Vector2 _size = new(8f, 8f);   // 가로(X)·세로(Z) m
        [SerializeField] private GameObject[] _plugs = new GameObject[4]; // N, E, S, W 막음벽
        [SerializeField] private Transform _depot;              // 목적지방만 — 식량 창고·상점 자리 (docs/09 새 루프)

        public NetworkVariable<byte> OpenSides = new(0);

        public Vector2 Size => _size;
        public Transform Depot => _depot;

        /// <summary>호스트: 스폰 전에 부른다. NavMesh를 굽기 전에 막음벽 상태가 맞아야 해서 바로 적용도 한다.</summary>
        public void ServerSetOpenSides(byte sides)
        {
            OpenSides.Value = sides;
            Apply(sides);
        }

        public override void OnNetworkSpawn()
        {
            Apply(OpenSides.Value);
            OpenSides.OnValueChanged += OnSidesChanged;
        }

        public override void OnNetworkDespawn() => OpenSides.OnValueChanged -= OnSidesChanged;

        private void OnSidesChanged(byte _, byte now) => Apply(now);

        private void Apply(byte sides)
        {
            for (int i = 0; i < 4 && i < _plugs.Length; i++)
                if (_plugs[i] != null) _plugs[i].SetActive((sides & (1 << i)) == 0);
        }

        /// <summary>면 i(0 N, 1 E, 2 S, 3 W)의 문 중심 (월드, 바닥 높이).</summary>
        public Vector3 DoorCenter(int side)
        {
            Vector3 local = side switch
            {
                0 => new Vector3(0f, 0f, _size.y * 0.5f),
                1 => new Vector3(_size.x * 0.5f, 0f, 0f),
                2 => new Vector3(0f, 0f, -_size.y * 0.5f),
                _ => new Vector3(-_size.x * 0.5f, 0f, 0f),
            };
            return transform.TransformPoint(local);
        }

#if UNITY_EDITOR
        public void EditorSetup(Vector2 size, GameObject[] plugs, Transform depot) { _size = size; _plugs = plugs; _depot = depot; }
        /// <summary>에디터 미리보기(네트워크 없이)용.</summary>
        public void EditorApply(byte sides) => Apply(sides);
#endif
    }
}
