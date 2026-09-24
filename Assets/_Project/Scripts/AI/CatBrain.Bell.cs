using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 고양이 방울 (고양이 140, plan cat-140): 상점 방울을 던져 맞히면 이번 스테이지 끝까지 목에 방울 — 지도(Tab)에 팀 모두에게 보인다.
    /// 행동은 그대로(맞아도 모름). `Belled`는 호스트만 쓰고, 목 방울·토스트는 각 클라가 값 변화로 그린다(추가 RPC 없음).
    /// </summary>
    public partial class CatBrain
    {
        private static readonly int BellColorId = Shader.PropertyToID("_BaseColor");

        public NetworkVariable<bool> Belled = new(false);
        private GameObject _bellView;

        /// <summary>호스트: 방울 달기. 이미 달렸으면 false.</summary>
        public bool ServerAttachBell()
        {
            if (!IsServer || Belled.Value) return false;
            Belled.Value = true;
            Log.Dev($"고양이 [{name}]: 방울 달림");
            return true;
        }

        // 모든 클라 — 클라는 OnNetworkSpawn에서 바로 꺼지므로 그 전에 부른다
        private void InitBellView()
        {
            Belled.OnValueChanged += OnBelledChanged;
            if (Belled.Value) ShowBell(announce: false); // 늦게 들어온 클라
        }

        public override void OnNetworkDespawn() => Belled.OnValueChanged -= OnBelledChanged;

        private void OnBelledChanged(bool _, bool now) { if (now) ShowBell(announce: true); }

        private void ShowBell(bool announce)
        {
            if (_bellView == null)
            {
                _bellView = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _bellView.name = "Bell";
                Destroy(_bellView.GetComponent<Collider>());
                _bellView.transform.SetParent(transform, false);
                _bellView.transform.localPosition = NeckLocal();
                _bellView.transform.localScale = Vector3.one * (0.14f / Mathf.Max(0.01f, transform.lossyScale.x));
                var block = new MaterialPropertyBlock();
                block.SetColor(BellColorId, new Color(1f, 0.78f, 0.15f));
                _bellView.GetComponent<Renderer>().SetPropertyBlock(block);
            }
            Log.Dev($"방울 연출: {name}"); // 2인 검증용
            if (announce) EventBus.RaiseCatBelled(transform.position);
        }

        // 목 = 주둥이(Muzzle) 바로 아래 — 몸 경계로 잡으면 꼬리까지 들어가 몸통 속에 묻혔다(시험 스크린샷). 주둥이가 없는 모델은 몸 경계 앞쪽
        private Vector3 NeckLocal()
        {
            var muzzle = transform.Find("Visual/Muzzle") ?? FindChild(transform, "Muzzle");
            if (muzzle != null && muzzle.TryGetComponent<Renderer>(out var mr))
                return transform.InverseTransformPoint(mr.bounds.center - Vector3.up * (mr.bounds.extents.y + 0.1f));
            var body = transform.Find("Visual");
            if (body != null && body.TryGetComponent<Renderer>(out var br))
                return transform.InverseTransformPoint(br.bounds.center + transform.forward * br.bounds.extents.z + Vector3.up * br.bounds.extents.y * 0.3f);
            return new Vector3(0f, 0.9f, 0.4f);
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == childName) return t;
            return null;
        }
    }
}
