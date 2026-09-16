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
        }

        private readonly List<Marker> _markers = new();
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
            marker.Rect.gameObject.SetActive(true);
        }

        private Marker CreateMarker(ulong owner)
        {
            var rect = Instantiate(_template, _template.parent);
            var nm = NetworkManager.Singleton;
            bool me = nm != null && owner == nm.LocalClientId;
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
            marker.Name.text = PlayerVisual.ColorNameFor(owner) + (me ? " (나)" : "");
            _markers.Add(marker);
            return marker;
        }

        private void Update()
        {
            var cam = Camera.main;
            float now = Time.unscaledTime;
            foreach (var m in _markers)
            {
                if (!m.Rect.gameObject.activeSelf) continue;
                float left = m.EndsAt - now;
                if (left <= 0f || cam == null)
                {
                    m.Rect.gameObject.SetActive(false);
                    continue;
                }
                m.Rect.anchoredPosition = ScreenAnchor.ToCanvas(cam, _canvasRect, m.World, EdgeMargins, out bool onScreen);
                m.Group.alpha = Mathf.Clamp01(left / FadeSeconds) * (onScreen ? 1f : OffscreenAlpha);
                string dist = $"{Mathf.RoundToInt(Vector3.Distance(cam.transform.position, m.World))}m";
                if (m.Distance.text != dist) m.Distance.text = dist;
            }
        }
    }
}
