using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 방 모듈 (docs/10 "조합 랜덤", 2026-09-24 고양이 57). 직사각형만 — 루트 BoxCollider(트리거)가 방 바운즈이고
    /// 접합 뒤 겹침 검사는 이것 하나로 한다. 스폰 지점은 빈 트랜스폼 표시(전리품·함정·고양이·쥐).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RoomModule : MonoBehaviour
    {
        public DoorSocket Entry;              // 이전 방과 접합 (쥐구멍방은 비어 있을 수 있음)
        public DoorSocket[] Exits;            // 1~2개 (2개면 하나는 보너스방용)
        public Transform[] LootSpawns;        // 6~12개
        public Transform[] TrapSpawns;        // 0~4개
        public Transform CatSpawn;            // 없으면 이 방엔 고양이 배정 안 함
        public Transform[] PlayerSpawns;      // 쥐구멍방만
        public Transform RatHole;             // 쥐구멍방만 — DepositZone 자리

        private BoxCollider _bounds;

        /// <summary>방 바운즈 (월드). 접합 뒤 Physics.SyncTransforms를 부른 다음 읽을 것.</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (_bounds == null) _bounds = GetComponent<BoxCollider>();
                return _bounds.bounds;
            }
        }

        /// <summary>바운즈 중심·반크기·회전 (OverlapBox용, 스케일 없는 루트 가정).</summary>
        public void GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            if (_bounds == null) _bounds = GetComponent<BoxCollider>();
            center = transform.TransformPoint(_bounds.center);
            halfExtents = _bounds.size * 0.5f;
            rotation = transform.rotation;
        }
    }
}
