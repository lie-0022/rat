using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RatGame.Player
{
    /// <summary>
    /// 소유 클라 카메라: 뒤따라오는 3인칭 + 마우스/오른스틱 궤도 회전 (docs/04 — 거리 3.5m, 정중앙).
    /// 씬의 Main Camera를 빌려 쓴다. Cinemachine 정식 리깅(어깨·충돌 처리)은 아트/폴리시 단계에서.
    /// </summary>
    public class PlayerCameraRig : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset _inputAsset;

        private Camera _cam;
        private InputAction _lookAction;
        private float _yaw;
        private float _pitch = 25f;

        private const float Distance = 3.5f;
        private const float HeightOffset = 0.4f;   // CameraTarget 위치와 일치
        private const float Sensitivity = 0.15f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _cam = Camera.main;
            _lookAction = _inputAsset.FindActionMap("Player", true).FindAction("Look", true);
            _yaw = transform.eulerAngles.y;
            SetCursorLocked(true); // 플레이 중 커서 잠금 — Esc로 해제 (DEV HUD 클릭용)
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) SetCursorLocked(false);
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
            // 커서가 풀려 있으면(메뉴 조작 중) 카메라 회전 정지 — 클릭이 새지 않게
            bool rotating = Cursor.lockState == CursorLockMode.Locked;

            Vector2 look = rotating ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            _yaw += look.x * Sensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * Sensitivity, -10f, 70f);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = transform.position + Vector3.up * HeightOffset;
            _cam.transform.position = focus - rot * Vector3.forward * Distance;
            _cam.transform.rotation = rot;
        }
    }
}
