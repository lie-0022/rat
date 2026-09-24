using RatGame.Core;
using RatGame.Data;
using RatGame.Run;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>
    /// 초인종 (design/cat-ideas/13·10, 2026-09-24). 쥐가 E 1s 홀드로 누르면(런당 1회) "딩동!" → 집주인이 현관으로 → 고양이도 따라 나간다.
    /// 관심 축의 최상위 카드 — 쥐가 집주인 이벤트를 **유발**한다. 판정·실행은 호스트(HouseEventDirector).
    /// </summary>
    public class Doorbell : NetworkBehaviour, IInteractable
    {
        [SerializeField] private BalanceConfigSO _balance;

        /// <summary>이미 눌렀다 (런당 1회 — 클라 프롬프트도 사라진다).</summary>
        public NetworkVariable<bool> Used = new NetworkVariable<bool>(false);

        public string PromptText => "초인종 누르기";
        public float HoldSeconds => _balance != null ? _balance.DoorbellHoldSeconds : 1f;
        public bool CanInteract(ulong clientId) => !Used.Value;

        public void ServerInteract(ulong clientId)
        {
            if (!IsServer || Used.Value) return;
            var house = RunManager.Instance != null ? RunManager.Instance.GetComponent<HouseEventDirector>() : null;
            if (house == null) { Log.Dev("초인종: 집주인 이벤트 디렉터 없음"); return; }
            Used.Value = true;
            house.ServerTrigger(HouseEventKind.Doorbell);
            Log.Dev($"초인종: client {clientId}가 누름");
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance) => _balance = balance;
#endif
    }
}
