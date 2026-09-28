using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 핑 마커 (docs/12 PingMarker, production/plans/ui-03). EventBus.PingReceived(로컬)를 구독해 오버레이 캔버스에 그린다 —
    /// 오버레이라 벽 뒤 지점도 그대로 보인다(투시). 팀색 마름모 + 이름 + 거리, 화면 밖이면 가장자리에 반투명으로 붙어 방향만 알려준다.
    /// 쥐마다 마커 하나 — 다시 찍으면 옮겨진다. 수명 pingMarkerSeconds, 마지막 0.5s 페이드.
    /// </summary>
    public class PingMarkerWidget : MonoBehaviour
    {
        private const float FadeSeconds = 0.5f;
        // 좌우 48, 아래는 인벤 슬롯·CarryInfo(~130) 위, 위는 RunBar(~110) 아래
        private static readonly ScreenAnchor.Margins EdgeMargins = new(48f, 48f, 170f, 130f);
        private const float OffscreenAlpha = 0.5f;

        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private RectTransform _template;
        [SerializeField] private Color _helpColor = new(1f, 0.35f, 0.3f, 1f); // 위기인 동료 표시 글자 (고양이 156)

        private sealed class Marker
        {
            public ulong Owner;
            public Vector3 World;
            public float EndsAt;
            public RectTransform Rect;
            public CanvasGroup Group;
            public UnityEngine.UI.Image Icon;
            public TMP_Text Name;
            public TMP_Text Distance;
            public Transform Follow;   // 고양이를 찍었으면 그 고양이 (고양이 165)
            public Vector3 FollowOffset;
        }

        private readonly List<Marker> _markers = new();
        private readonly Dictionary<ulong, Marker> _help = new(); // 위기인 동료 표시 — 쥐마다 하나 (고양이 156)
        private readonly HashSet<ulong> _helpSeen = new();
        private float _nextHelpScan;
        private const float HelpScanSeconds = 0.25f;
        private const float HelpHeight = 1.2f;
        private const float CatPingSnap = 0.6f; // 핑이 고양이 몸 이만큼 안이면 고양이를 찍은 것 (고양이 165)
        private const float HelpNearMax = 4f, HelpNearMin = 1.5f, HelpNearAlpha = 0.25f; // 가까이 가면 옅게 (고양이 161)
        private float _lastHelpAlpha = -1f;
        private RectTransform _canvasRect;

        private void Awake()
        {
            _template.gameObject.SetActive(false);
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        }

        private void OnEnable() => EventBus.PingReceived += OnPing;
        private void OnDisable() => EventBus.PingReceived -= OnPing;

        private void OnPing(ulong owner, Vector3 world)
        {
            var marker = _markers.Find(m => m.Owner == owner) ?? CreateMarker(owner);
            marker.World = world;
            marker.EndsAt = Time.unscaledTime + (_balance != null ? _balance.PingMarkerSeconds : 3f);
            var cat = CatAt(world);
            marker.Follow = cat != null ? cat.transform : null; // 고양이는 움직이니 마커가 따라간다 (고양이 165)
            marker.FollowOffset = cat != null ? world - cat.transform.position : Vector3.zero;
            marker.Name.text = WhoText(owner) + (cat != null ? CatText(cat) : ItemText(world));
            marker.Rect.gameObject.SetActive(true);
        }

        private static string WhoText(ulong owner)
        {
            var nm = NetworkManager.Singleton;
            return PlayerVisual.ColorNameFor(owner) + (nm != null && owner == nm.LocalClientId ? " (나)" : "");
        }

        // 음식을 찍으면 이름·가치까지 (고양이 139) — 물건 위치·가치는 모두에게 이미 있어 각자 계산, 동기화 없음
        private string ItemText(Vector3 world)
        {
            float reach = _balance != null ? _balance.PingItemSnapMeters : 0.6f;
            World.CarryableItem best = null; float bestD = reach * reach;
            foreach (var item in FindObjectsByType<World.CarryableItem>(FindObjectsSortMode.None))
            {
                if (item.Data == null || item.Pocketed.Value) continue;
                var col = item.GetComponentInChildren<Collider>();
                Vector3 near = col != null ? col.ClosestPoint(world) : item.transform.position; // 큰 물건은 중심이 멀다 — 표면 기준
                float d = (near - world).sqrMagnitude;
                if (d < bestD) { bestD = d; best = item; }
            }
            if (best == null) return "";
            int value = best.EffectiveValue;
            Log.Dev($"핑 물건 연출: {best.Data.DisplayName} {value}"); // 2인 검증용
            return value > 0 ? $" · {best.Data.DisplayName} {value}" : $" · {best.Data.DisplayName}";
        }

        // 핑 지점이 고양이 몸 가까이면 그 고양이 (고양이 165)
        private static AI.CatBrain CatAt(Vector3 world)
        {
            AI.CatBrain best = null; float bestD = CatPingSnap * CatPingSnap;
            foreach (var c in FindObjectsByType<AI.CatBrain>(FindObjectsSortMode.None))
            {
                // 충돌체는 세운 캡슐 하나라 길쭉한 머리·꼬리가 밖에 있다 — 보이는 몸(렌더러 경계)으로 본다 (고양이 166)
                float d = float.MaxValue;
                foreach (var r in c.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.gameObject.activeInHierarchy) d = Mathf.Min(d, r.bounds.SqrDistance(world));
                if (d == float.MaxValue) d = (c.transform.position - world).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        private static string CatText(AI.CatBrain cat)
        {
            var p = cat.Personality;
            string who = p == null ? "고양이" : p.IsGuard ? "문지기" : p.IsPatroller ? "순찰꾼" : p.IsKitten ? "아기 고양이" : "고양이";
            Log.Dev($"핑 고양이 연출: {who}"); // 2인 검증용
            return $" · {who}!";
        }

        private Marker CreateMarker(ulong owner)
        {
            var marker = BuildMarker(owner);
            _markers.Add(marker);
            return marker;
        }

        private Marker BuildMarker(ulong owner)
        {
            var rect = Instantiate(_template, _template.parent);
            var marker = new Marker
            {
                Owner = owner,
                Rect = rect,
                Group = rect.GetComponent<CanvasGroup>(),
                Icon = rect.Find("Icon").GetComponent<UnityEngine.UI.Image>(),
                Name = rect.Find("NameBox/Name").GetComponent<TMP_Text>(),
                Distance = rect.Find("DistanceBox/Distance").GetComponent<TMP_Text>()
            };
            marker.Icon.color = PlayerVisual.ColorFor(owner);
            return marker;
        }

        // 위기인 동료 (고양이 156) — 잡힘·다운·끈끈이면 구해질 때까지 그 쥐 위에. 상태·위치는 이미 모두에게 있어 각자 계산
        private void ScanHelp()
        {
            _helpSeen.Clear();
            foreach (var p in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None))
            {
                if (p.IsOwner) continue;
                string label = p.State.Value switch
                {
                    ConditionState.Downed => "다운",
                    ConditionState.Trapped => "끈끈이",
                    ConditionState.Pinned => "잡힘",
                    _ => null
                };
                if (label == null) continue;
                ulong id = p.OwnerClientId;
                _helpSeen.Add(id);
                if (!_help.TryGetValue(id, out var m))
                {
                    m = BuildMarker(id);
                    _help[id] = m;
                }
                m.World = p.transform.position + Vector3.up * HelpHeight; // 다운이면 시점(플레이어)이 끌려가는 몸을 따라간다 — 같은 자리
                string text = $"{PlayerVisual.ColorNameFor(id)} · {label}";
                if (m.Name.text != text) { m.Name.text = text; Log.Dev($"동료 위기 표시: {text}"); } // 2인 검증용
                if (!m.Rect.gameObject.activeSelf)
                {
                    m.Rect.gameObject.SetActive(true);
                    m.Name.color = _helpColor; // 켠 뒤에 — 템플릿의 ThemedGraphic이 처음 켜질 때(Awake) 테마 색으로 덮는다
                }
            }
            foreach (var kv in _help)
                if (!_helpSeen.Contains(kv.Key) && kv.Value.Rect.gameObject.activeSelf)
                {
                    kv.Value.Rect.gameObject.SetActive(false);
                    kv.Value.Name.text = "";
                    Log.Dev($"동료 위기 표시 끝: {PlayerVisual.ColorNameFor(kv.Key)}");
                }
        }

        private void Update()
        {
            var cam = Camera.main;
            float now = Time.unscaledTime;
            if (now >= _nextHelpScan) { _nextHelpScan = now + HelpScanSeconds; ScanHelp(); }
            foreach (var m in _help.Values)
            {
                if (!m.Rect.gameObject.activeSelf || cam == null) continue;
                m.Rect.anchoredPosition = ScreenAnchor.ToCanvas(cam, _canvasRect, m.World, EdgeMargins, out bool onScreen);
                float meters = Vector3.Distance(cam.transform.position, m.World);
                // 바로 옆이면 옅게 — 구출할 몸을 가리지 않게 (고양이 161). 이름은 남긴다
                float near = Mathf.InverseLerp(HelpNearMin, HelpNearMax, meters);
                m.Group.alpha = (onScreen ? 1f : OffscreenAlpha) * Mathf.Lerp(HelpNearAlpha, 1f, near);
                if (Mathf.Abs(m.Group.alpha - _lastHelpAlpha) > 0.2f) { _lastHelpAlpha = m.Group.alpha; Log.Dev($"동료 위기 표시 알파: {m.Group.alpha:0.00} ({meters:0.0}m)"); } // 2인 검증용
                string dist = $"{Mathf.RoundToInt(meters)}m";
                if (m.Distance.text != dist) m.Distance.text = dist;
            }
            foreach (var m in _markers)
            {
                if (!m.Rect.gameObject.activeSelf) continue;
                float left = m.EndsAt - now;
                if (left <= 0f || cam == null)
                {
                    m.Rect.gameObject.SetActive(false);
                    continue;
                }
                if (m.Follow != null) m.World = m.Follow.position + m.FollowOffset;
                m.Rect.anchoredPosition = ScreenAnchor.ToCanvas(cam, _canvasRect, m.World, EdgeMargins, out bool onScreen);
                m.Group.alpha = Mathf.Clamp01(left / FadeSeconds) * (onScreen ? 1f : OffscreenAlpha);
                string dist = $"{Mathf.RoundToInt(Vector3.Distance(cam.transform.position, m.World))}m";
                if (m.Distance.text != dist) m.Distance.text = dist;
            }
        }
    }
}
