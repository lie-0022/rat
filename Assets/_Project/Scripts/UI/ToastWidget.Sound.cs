using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 토스트 — 소리 대신 자막(오디오 전): 고양이 소리(루틴 예고·코골이·아기·졸음, 방향 붙음)·동료 찍찍, 방향·거리 글자 도우미.
    /// ToastWidget.cs가 300줄을 넘어 나눔. 구독은 본체 OnEnable/OnDisable.
    /// </summary>
    public partial class ToastWidget
    {
        [SerializeField] private BalanceConfigSO _balance; // 예고 들리는 거리
        private string _lastCue;

        // 고양이 루틴 예고 (design/cat-ideas/02) — 소리 대신 자막. 고양이 가까이 있는 쥐만 듣는다, 같은 문구 연속 금지
        private void OnCatCue(CatCueKind kind, Vector3 catPos, float hearScale)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
            float range = (_balance != null ? _balance.CatCueHearRange : 18f) * Mathf.Max(1f, hearScale); // 벽 속 큰 고양이 18 → 34m — 방이 2배라 예전처럼 방 두 개쯤
            if (Vector3.Distance(nm.LocalClient.PlayerObject.transform.position, catPos) > range) return;
            string text = kind switch
            {
                CatCueKind.Food => "(배 꼬르륵 — 고양이가 밥 먹으러)",
                CatCueKind.Litter => "(모래 긁는 소리… 곧 우다다!)",
                CatCueKind.Sun => "(창가 쪽 기지개 — 햇볕 쬐러)",
                CatCueKind.Bed => "(쩌억 하품 — 자러 간다)",
                CatCueKind.Water => "(할짝할짝 — 물 마시러)",
                CatCueKind.Ambush => "(츄릅… 어디선가)",
                CatCueKind.KittenCall => "(냐앙! — 아기 고양이가 엄마를 부른다)",
                CatCueKind.GuardNap => "(쿨쿨… 문지기가 졸고 있다 — 지금!)",
                CatCueKind.PatrolNap => "(꾸벅꾸벅… 순찰꾼이 잠들었다 — 큰길이 빈다)",
                CatCueKind.Snore => "(드르렁… 고양이가 깊이 잠들었다)",
                CatCueKind.SnoreStop => "(코골이가 멈췄다 — 곧 깬다!)",
                _ => null
            };
            if (text == null || text == _lastCue) return;
            _lastCue = text;
            if (kind != CatCueKind.Ambush) text += " · " + DirectionFrom(catPos); // 매복은 어디서인지 모르는 게 핵심 (고양이 153)
            Log.Dev($"고양이 예고: {text}");
            Show(text, UiColorRole.Secondary);
        }

        private readonly Dictionary<ulong, float> _lastSqueak = new();
        private const float SqueakToastGap = 1.5f; // 연타 도배 방지 — 표시만

        // 동료 찍찍 (고양이 158) — 오디오 전 자막. 소통 수단이라 어디서 났는지까지
        private void OnRatSqueak(ulong owner, Vector3 pos)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || owner == nm.LocalClientId || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
            float range = _balance != null ? _balance.SqueakHearMeters : 30f;
            if (Vector3.Distance(nm.LocalClient.PlayerObject.transform.position, pos) > range) return;
            if (_lastSqueak.TryGetValue(owner, out float last) && Time.unscaledTime - last < SqueakToastGap) return;
            _lastSqueak[owner] = Time.unscaledTime;
            string text = $"(찍찍!) {PlayerVisual.ColorNameFor(owner)} · {Where(pos)}";
            Log.Dev($"동료 찍찍 자막: {text}"); // 2인 검증용
            Show(text, UiColorRole.Secondary);
        }

        // 동료 위기 알림 둘째 줄 — "오른쪽 뒤 23m" (고양이 155)
        private static string Where(Vector3 target)
        {
            var nm = NetworkManager.Singleton;
            var me = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (me == null) return DirectionFrom(target);
            return $"{DirectionFrom(target)} {Mathf.RoundToInt(Vector3.Distance(me.transform.position, target))}m";
        }

        private static readonly string[] Directions = { "앞", "오른쪽 앞", "오른쪽", "오른쪽 뒤", "뒤", "왼쪽 뒤", "왼쪽", "왼쪽 앞" };

        // 소리 자막의 방향 — 진짜 소리처럼 어느 쪽인지 (고양이 153). 내 카메라 기준 8방향, 들은 순간 한 번
        private static string DirectionFrom(Vector3 source)
        {
            var cam = Camera.main;
            if (cam == null) return Directions[0];
            Vector3 to = source - cam.transform.position; to.y = 0f;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (to.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.0001f) return Directions[0];
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up); // 오른쪽이 +
            int i = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
            return Directions[i];
        }

        private float _lastSniffHintAt = -10f;

        // 킁킁 쿨다운 중 누름 (고양이 162) — 1초에 한 번만
        private void OnSniffCooldown(float secondsLeft)
        {
            if (Time.unscaledTime - _lastSniffHintAt < 1f) return;
            _lastSniffHintAt = Time.unscaledTime;
            string text = $"(킁킁… 코가 아직 얼얼 — {Mathf.CeilToInt(secondsLeft)}초)";
            Log.Dev($"킁킁 쿨다운 안내: {text}");
            Show(text, UiColorRole.Secondary, 1.5f);
        }
    }
}
