using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.Player
{
    /// <summary>
    /// 스태미나 (docs/04). 소유 클라 시뮬(표시용) — sprint 20/s, 무거운 운반 15/s, 미소모 1s 후 25/s 회복.
    /// 0 도달: sprint 불가 + 1.5s 헐떡임(소음 18 — 호스트 Emit 필요라 ServerRpc).
    /// 치즈 먹기 회복(Edible)은 치즈 아이템 생기는 태스크 1-7에서.
    /// </summary>
    public class PlayerStamina : NetworkBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;

        public float Current { get; private set; }

        /// <summary>소유 클라: 즉시 회복 (치즈 먹기 — World/EdibleItem, docs/04).</summary>
        public void Restore(float amount) => Current = Mathf.Min(Max, Current + amount);

        private PlayerController _movement;
        private PlayerCarryController _carry;
        private PlayerUpgrades _upgrades;
        private float _lastDrainTime;
        private float _pantEndTime;

        /// <summary>최대치 — 상점 업그레이드 배율 포함 (docs/11).</summary>
        public float Max => _balance.StaminaMax * (_upgrades != null ? _upgrades.StaminaMaxMultiplier : 1f);

        /// <summary>지친 상태 — 0이거나 헐떡이는 중 (달리기 불가). HUD 링 색 표시용.</summary>
        public bool IsExhausted => Current <= 0f || Time.time < _pantEndTime;

        private void Awake()
        {
            _movement = GetComponent<PlayerController>();
            _carry = GetComponent<PlayerCarryController>();
            _upgrades = GetComponent<PlayerUpgrades>();
        }

        public override void OnNetworkSpawn()
        {
            Current = Max;
            if (!IsOwner) enabled = false;
        }

        private void Update()
        {
            float drain = 0f;
            if (_movement.IsSprinting) drain += _balance.StaminaSprintDrain;
            if (_carry != null && _carry.CurrentLoadPerRat > _balance.SprintBlockLoad)
                drain += _balance.StaminaHeavyDrain; // 2인 필요 아이템 단독 시도 포함 (docs/04)

            if (drain > 0f)
            {
                bool wasPositive = Current > 0f;
                Current = Mathf.Max(0f, Current - drain * Time.deltaTime);
                _lastDrainTime = Time.time;
                if (wasPositive && Current <= 0f)
                {
                    _pantEndTime = Time.time + _balance.ExhaustPantSeconds;
                    PantServerRpc(); // 헐떡임 소음은 호스트가 낸다
                }
            }
            else if (Time.time - _lastDrainTime >= _balance.StaminaRegenDelay)
            {
                Current = Mathf.Min(Max, Current + _balance.StaminaRegen * Time.deltaTime);
            }

            _movement.BlockSprintStamina = IsExhausted;
        }

        [ServerRpc]
        private void PantServerRpc()
        {
            Noise.NoiseSystem.Emit(transform.position, _balance.PantLoudness,
                Noise.NoiseType.Footstep, OwnerClientId);
            Core.Log.Dev($"헐떡임: client {OwnerClientId}");
        }
    }
}
