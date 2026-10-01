using System.Collections.Generic;
using RatGame.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 세게 던져도 바닥·벽을 안 뚫는다 (고양이 342) — 바닥은 두께 0.1m 상자, 물건 충돌은 Discrete라 빠르면 한 스텝에 뚫을 수 있다.
    /// 벽 속 방 하나에서 작은 전리품 30개를 바닥·벽 쪽으로 15m/s(게임에서 날 수 있는 가장 빠른 속도 약 10m/s의 1.5배)로 쏘고 1.5초 뒤 방 밖·바닥 아래를 센다.
    /// 물건엔 떨어짐 구조가 없어 뚫으면 그 물건은 영영 잃는다.
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Throw Through Floor·Wall")]
        private static void ArmThrowStress() => Arm("throwstress");

        // 실제로 날 수 있는 가장 빠른 속도 ≈ 10m/s(만충 6m/s × 던지기 업그레이드 + 포물선 꼭대기에서 떨어짐) — 1.5배 여유로 15m/s.
        // 막기 전: 15m/s면 바닥(0.1m)을 30개 중 1개꼴, 11m/s에서도 벽(0.2m)을 3판 중 1판에 2개 뚫었다 → 던질 때 연속 충돌(CarryableItem.BeginFlight, 고양이 342)
        private const float ShotSpeed = 15f;
        private static readonly List<Rigidbody> _shot = new();
        private static readonly System.Reflection.MethodInfo BeginFlight =
            typeof(CarryableItem).GetMethod("BeginFlight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static Bounds _shotRoom;

        /// <summary>기지 발판으로 벽 속 스테이지까지 (기지 창 글자 시험과 같은 길).</summary>
        private static Step ToWalls(string name) => new Step { Name = name, Ready = () =>
        {
            if (StageUp() || EditorApplication.timeSinceStartup - _stepAt > 60) return true;
            var pad = Object.FindFirstObjectByType<DeparturePad>();
            if (pad != null && pad.IsSpawned && SceneManager.GetActiveScene().name == "Hub")
            {
                for (int i = 0; i < 4 && !pad.DestinationName.Contains("벽 속"); i++) pad.ServerCycleDestination();
                Put(pad.transform.position + Vector3.up * 0.5f);
            }
            return false;
        }, Check = () => StageUp() ? null : "벽 속 스테이지가 안 열림" };

        private static List<Step> ThrowStressSteps() => new()
        {
            HostFromMenu("호스트"), InHub("기지"), ToWalls("벽 속 도착"),
            new Step { Name = "쏘기", Wait = 2f, Act = () =>
            {
                _shot.Clear();
                RoomModule room = null;
                foreach (var r in Object.FindObjectsByType<RoomModule>(FindObjectsSortMode.None))
                    if (room == null || r.WorldBounds.size.x * r.WorldBounds.size.z > room.WorldBounds.size.x * room.WorldBounds.size.z) room = r; // 넓은 방
                _shotRoom = room.WorldBounds;
                Vector3 c = _shotRoom.center; float floor = _shotRoom.min.y, half = _shotRoom.extents.x - 1.5f;
                // 문이 없는 벽 쪽을 고른다 — 문 쪽으로 쏘면 뚫은 게 아니라 문으로 나간다(첫 판: "벽 밖 10")
                int mask = LayerMask.GetMask("RoomStatic", "NoiseBlocker");
                Vector3 dir = Vector3.right, side = Vector3.forward;
                foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    Vector3 s2 = Vector3.Cross(Vector3.up, d), from = c + d * half + Vector3.up * (floor + 0.6f - c.y);
                    bool solid = true;
                    for (int i = 0; i < 10 && solid; i++)
                        solid = Physics.Raycast(from + s2 * ((i - 5) * 0.5f), d, 3f, mask, QueryTriggerInteraction.Ignore);
                    if (solid) { dir = d; side = s2; break; }
                }
                string[] ids = { "loot_candy", "loot_coin", "loot_cheese_bit" };
                for (int k = 0; k < ids.Length; k++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Items/Loot/{ids[k]}.prefab");
                    for (int i = 0; i < 10; i++)
                    {
                        bool down = i % 2 == 0;
                        // 바닥으로 쏘는 건 방 가운데서, 벽으로 쏘는 건 벽 1.5m 앞에서 벽을 향해
                        Vector3 at = down ? new Vector3(c.x + (i - 5) * 0.6f, floor + 1.2f, c.z + (k - 1) * 0.8f)
                                          : new Vector3(c.x, floor + 0.6f, c.z) + dir * half + side * ((i - 5) * 0.5f + k * 0.15f);
                        var go = Object.Instantiate(prefab, at, Quaternion.identity);
                        go.GetComponent<NetworkObject>().Spawn(true);
                        var rb = go.GetComponent<Rigidbody>();
                        rb.linearVelocity = down ? new Vector3(0f, -ShotSpeed, 0f) : dir * ShotSpeed + Vector3.down * 2f;
                        BeginFlight.Invoke(go.GetComponent<CarryableItem>(), null); // 실제 던지기와 같은 시작 처리(연속 충돌) — 그래야 게임의 막음을 시험한다
                        _shot.Add(rb);
                    }
                }
            } },
            new Step { Name = "뚫음", Wait = 1.5f, Check = () =>
            {
                var b = _shotRoom; b.Expand(new Vector3(1f, 2f, 1f)); // 벽 두께·튐 여유
                int below = 0, escaped = 0, gone = 0;
                foreach (var rb in _shot)
                {
                    if (rb == null) { gone++; continue; }
                    Vector3 p = rb.position;
                    if (p.y < _shotRoom.min.y - 0.3f) below++;
                    else if (!b.Contains(p)) escaped++;
                }
                Report.Append($" | 30개 {ShotSpeed:0}m/s → 바닥 아래 {below} · 벽 밖 {escaped} · 사라짐 {gone}");
                return below + escaped == 0 ? null : "세게 던진 물건이 바닥·벽을 뚫음";
            } },
        };
    }
}
