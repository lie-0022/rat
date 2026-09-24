using RatGame.AI;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    public enum LureKind { Yarn, Catnip, Crumbs }

    /// <summary>
    /// 유인 아이템 (docs/07·08, 호스트 판정). Chase보다 우선순위 낮음 — 추격 중엔 안 속는다(CatBrain에서 거른다).
    ///   Yarn(털실뭉치): 던져져 착지한 지점 기준 반경 내 고양이 Distracted 8s → 소모
    ///   Catnip(캣닢): 설치 후 고양이가 3m 진입 시 Distracted 15s, 1회 → 소모
    ///   Crumbs(쿠키 부스러기): 설치 지점을 순찰 경로에 1회 삽입 → 소모
    /// 상점 구매·소지 슬롯(숫자키)은 태스크 2-4 — 지금은 월드 배치/던지기로 동작.
    /// </summary>
    public class CatLure : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private LureKind _kind;

        private CarryableItem _carryable; // Yarn만 사용
        private bool _consumed;

        private void Awake() => _carryable = GetComponent<CarryableItem>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer) { enabled = false; return; }
            if (_kind == LureKind.Crumbs)
            {
                foreach (var brain in FindCats())
                    brain.ServerInsertWaypoint(transform.position);
                Consume(); // 설치 즉시 경로 삽입 후 소모 (docs/08)
            }
        }

        private void FixedUpdate()
        {
            if (_consumed || _kind != LureKind.Catnip) return;
            foreach (var brain in FindCats())
            {
                if (Vector3.Distance(brain.transform.position, transform.position) > _balance.LureCatnipRadius) continue;
                brain.ServerDistract(transform.position, _balance.LureCatnipSeconds, wobbleAfter: true); // 취한 뒤 비틀거림
                Log.Dev($"캣닢 발동: {brain.name}");
                Consume();
                return;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || _consumed || _kind != LureKind.Yarn) return;
            if (_carryable == null || !_carryable.IsRecentlyThrown) return;

            bool any = false;
            foreach (var brain in FindCats())
            {
                if (Vector3.Distance(brain.transform.position, transform.position) > _balance.LureYarnRadius) continue;
                brain.ServerDistract(transform.position, _balance.LureYarnSeconds, fooledBy: _carryable.AttributedClient); // 속은 걸 알면 던진 쥐를 찍는다
                any = true;
            }
            if (any)
            {
                Log.Dev("털실뭉치 발동");
                Consume();
            }
        }

        private static CatBrain[] FindCats() =>
            Object.FindObjectsByType<CatBrain>(FindObjectsSortMode.None);

        // 씬에 놓인 캣닢·부스러기는 CarryableItem이 없어 여기서 끈다 — 파괴 대신 디스폰만 되므로 (고양이 134)
        public override void OnNetworkDespawn()
        {
            if (NetworkObject.IsSceneObject == true) gameObject.SetActive(false);
        }

        private void Consume()
        {
            _consumed = true;
            NetworkObject.DespawnSafe();
        }
    }
}
