using System.Collections.Generic;
using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 어둠 구역 (docs/07 "어둠", docs/10 DarkZones — 2026-09-24 고양이 31). 박스 트리거 안의 쥐는 고양이 시야 거리가 줄어든다
    /// (CatSenses가 쥐 위치로 IsDark를 묻는다). Lit = 불 켜짐(고양이 32) — 켜진 동안은 어둠이 아니다.
    /// 바닥의 어두운 판(_floorMark)이 곧 표시 — 모든 클라가 Lit에 따라 숨긴다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LightZone : NetworkBehaviour
    {
        [SerializeField] private Renderer _floorMark;

        public NetworkVariable<bool> Lit = new NetworkVariable<bool>(false);

        private static readonly List<LightZone> _all = new();
        private BoxCollider _box;
        private float _litUntil = -1f;

        public static IReadOnlyList<LightZone> All => _all;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public override void OnNetworkSpawn()
        {
            Lit.OnValueChanged += OnLitChanged;
            ApplyVisual();
        }

        public override void OnNetworkDespawn() => Lit.OnValueChanged -= OnLitChanged;

        private void OnLitChanged(bool _, bool lit)
        {
            ApplyVisual();
            Log.Dev($"어둠 연출: {name} {(lit ? "불 켜짐" : "어둠")}"); // 2인 검증용
        }

        private void ApplyVisual()
        {
            if (_floorMark != null) _floorMark.enabled = !Lit.Value;
        }

        /// <summary>이 구역 박스 안인가 (높이 무관 — 바닥 위 쥐 판정).</summary>
        public bool Contains(Vector3 pos)
        {
            Vector3 local = transform.InverseTransformPoint(pos) - _box.center;
            Vector3 half = _box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>꺼진(어두운) 구역 안인가.</summary>
        public static bool IsDark(Vector3 pos)
        {
            foreach (var z in _all)
                if (!z.Lit.Value && z.Contains(pos)) return true;
            return false;
        }

        /// <summary>구역 바닥 중심 (고양이가 오는 곳).</summary>
        public Vector3 Center { get { var c = transform.TransformPoint(_box.center); c.y = transform.position.y; return c; } }

        /// <summary>호스트: 집주인이 불을 켰다 — seconds 뒤 다시 어둠 + End 알림 (고양이 32).</summary>
        public void ServerLightFor(float seconds)
        {
            if (!IsServer) return;
            _litUntil = Time.time + seconds;
            ServerSetLit(true);
        }

        private void Update()
        {
            if (!IsServer || _litUntil < 0f || Time.time < _litUntil) return;
            _litUntil = -1f;
            ServerSetLit(false);
            if (Run.RunManager.Instance != null) Run.RunManager.Instance.ServerHouseEvent(HouseEventKind.LightOn, HouseEventPhase.End);
        }

        /// <summary>호스트: 불 켜기·끄기 (고양이 32 집주인 이벤트).</summary>
        public void ServerSetLit(bool lit)
        {
            if (!IsServer) return;
            Lit.Value = lit;
            Log.Dev($"어둠: {name} {(lit ? "불 켜짐" : "다시 어둠")}");
        }

#if UNITY_EDITOR
        public void EditorSetup(Renderer floorMark) => _floorMark = floorMark;
#endif
    }
}
