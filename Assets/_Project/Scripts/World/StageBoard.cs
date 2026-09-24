using RatGame.Core;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 기지 목적지 게시판 (고양이 59, docs/11). E로 출발 발판의 목적지를 돌린다 — 창고(손으로 만든 데모 맵) ↔ 부엌(존 생성기, docs/10).
    /// 판정은 호스트(DeparturePad.ServerCycleDestination), 글씨는 발판의 Destination NV를 읽어 각자 그린다.
    /// </summary>
    public class StageBoard : NetworkBehaviour, IInteractable
    {
        [SerializeField] private DeparturePad _pad;
        [SerializeField] private TMP_Text _label;

        public string PromptText => "목적지 바꾸기";
        public float HoldSeconds => 0f;
        public bool CanInteract(ulong clientId) => _pad != null && _pad.IsSpawned && !_pad.Counting.Value;

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer || _pad == null) return;
            if (_pad.ServerCycleDestination()) Log.Dev($"목적지 게시판: client {clientId}가 {_pad.DestinationName}(으)로");
        }

        private void Update()
        {
            if (_label == null || _pad == null || !_pad.IsSpawned) return;
            string blurb = _pad.DestinationBlurb;
            string text = $"[E] 목적지\n<size=130%>{_pad.DestinationName}</size>{(blurb.Length > 0 ? $"\n<size=60%>{blurb}</size>" : "")}";
            if (_label.text != text) _label.text = text; // 같으면 안 건드린다 — 메시 재생성 방지
        }

#if UNITY_EDITOR
        public void EditorSetup(DeparturePad pad, TMP_Text label) { _pad = pad; _label = label; }
#endif
    }
}
