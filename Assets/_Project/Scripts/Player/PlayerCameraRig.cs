using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace RatGame.Player
{
    /// <summary>
    /// 소유 클라 1인칭 카메라 (2026-09-12 결정: 1인칭 + 손만 보임). 씬의 Main Camera를 눈 위치에 붙인다.
    /// 몸은 카메라 yaw를 따라 돈다(PlayerController가 Yaw를 읽음). 내 몸 메시는 그림자만 남기고 숨김.
    /// 손 모델·팔은 아트 단계(Ray)에서 — 지금은 빈 시야.
    /// </summary>
    public class PlayerCameraRig : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset _inputAsset;

        private Camera _cam;
        private InputAction _lookAction;
        private float _yaw;
        private float _pitch;

        /// <summary>카메라 수평 회전 — 몸 방향의 원천 (PlayerController가 매 FixedUpdate 맞춤).</summary>
        public float Yaw => _yaw;

        private const float EyeHeight = 0.45f;     // 캡슐 중심 기준 (월드) — 머리 꼭대기 바로 아래
        private const float EyeForward = 0.12f;    // 캡슐 앞면 근처 — 자기 콜라이더 안쪽에서 렌더 안 되게
        private const float Sensitivity = 0.15f;
        private const float PitchLimit = 75f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _cam = Camera.main;
            _lookAction = _inputAsset.FindActionMap("Player", true).FindAction("Look", true);
            _yaw = transform.eulerAngles.y;
            _pitch = 0f;
            HideOwnBody();
            SetCursorLocked(true); // 플레이 중 커서 잠금 — Esc로 해제 (DEV HUD 클릭용)
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) SetCursorLocked(false);
        }

        // 1인칭에서 자기 몸이 카메라를 가리지 않게 — 그림자는 남긴다 (다른 플레이어에겐 정상 표시)
        private void HideOwnBody()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; return; }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            // 커서가 풀려 있으면(메뉴 조작 중) 시선 회전 정지 — 클릭이 새지 않게
            bool rotating = Cursor.lockState == CursorLockMode.Locked;

            Vector2 look = rotating ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            _yaw += look.x * Sensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * Sensitivity, -PitchLimit, PitchLimit);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 eye = transform.position + Vector3.up * EyeHeight
                          + Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * EyeForward;
            _cam.transform.SetPositionAndRotation(eye, rot);
        }
    }
}
