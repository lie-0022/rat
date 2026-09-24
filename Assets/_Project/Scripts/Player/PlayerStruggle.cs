using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 버둥거리기 (design/cat-ideas/04, 2026-09-24 고양이 35). 고양이에게 잡혀(Pinned) 있을 때 A·D를 번갈아 누르면 버둥.
    /// 소유 클라는 좌우 부호가 바뀔 때마다 알리기만 하고, 효과(보는 쪽으로 굴러감·관심 감소)는 호스트 CatBrain이 정한다.
    /// </summary>
    public class PlayerStruggle : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction _moveAction;
        private PlayerCondition _condition;
        private int _lastSign;

        // 호스트
        private float _lastStruggleTime = -99f;
        private int _presses;

        /// <summary>호스트: 1s 안에 버둥거렸나.</summary>
        public bool RecentlyStruggling => Time.time - _lastStruggleTime < 1f;

        /// <summary>호스트: 쌓인 버둥 횟수를 가져가고 0으로.</summary>
        public int ConsumePresses() { int n = _presses; _presses = 0; return n; }

        private void Awake() => _condition = GetComponent<PlayerCondition>();

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            _moveAction = _inputAsset.FindActionMap("Player", true).FindAction("Move", true);
        }

        private void Update()
        {
            if (!IsOwner || _moveAction == null || _condition == null) return;
            if (_condition.State.Value != ConditionState.Pinned || InputFocus.IsUiOpen) { _lastSign = 0; return; }
            float x = _moveAction.ReadValue<Vector2>().x;
            int sign = x > 0.5f ? 1 : x < -0.5f ? -1 : 0;
            if (sign == 0 || sign == _lastSign) return; // 손을 뗐다 같은 쪽을 다시 누르는 건 버둥이 아니다
            bool alternated = _lastSign != 0;
            _lastSign = sign;
            if (alternated) StruggleServerRpc();
        }

        [ServerRpc]
        private void StruggleServerRpc()
        {
            if (_condition == null || _condition.State.Value != ConditionState.Pinned) return;
            if (Time.time - _lastStruggleTime < _balance.CatToyStruggleMinInterval) return; // 매크로 연타 거름
            _lastStruggleTime = Time.time;
            _presses++;
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance, InputActionAsset input) { _balance = balance; _inputAsset = input; }
#endif
    }
}
