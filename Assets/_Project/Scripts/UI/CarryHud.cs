using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 소유 클라 HUD 생성기 — 스폰 시 Hud 프리팹(HudController)을 만들어 내 플레이어에 Bind, 디스폰 시 파괴.
    /// HUD 내용은 전부 uGUI 위젯(docs/12)에 있다. 시스템 상태를 읽기만 한다 — 호출 없음 (CLAUDE.md 규칙 3).
    /// </summary>
    public class CarryHud : NetworkBehaviour
    {
        [SerializeField] private HudController _hudPrefab;

        private HudController _hud;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            if (_hudPrefab == null) return;
            // 플레이어는 씬 전환에도 살아남으므로 HUD도 같이 유지
            _hud = Instantiate(_hudPrefab);
            DontDestroyOnLoad(_hud.gameObject);
            _hud.Bind(GetComponent<PlayerCarryController>(), GetComponent<PlayerStamina>());
        }

        public override void OnNetworkDespawn()
        {
            if (_hud != null) Destroy(_hud.gameObject);
        }
    }
}
