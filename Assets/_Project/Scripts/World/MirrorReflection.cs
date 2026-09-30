using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace RatGame.World
{
    /// <summary>
    /// 거울 평면 반사 (docs/11 거울, 각 클라 로컬). 거울 면 뒤로 뒤집은 눈 위치에서 거울 사각형을 창으로 삼는
    /// off-axis 카메라로 방을 그려 유리 Quad의 텍스처로 쓴다 — 면 위 한 점이 텍스처 한 점과 1:1이라 커스텀 셰이더 없이 URP Unlit으로 된다.
    /// 근평면 = 거울 면이라 거울 뒤(액자·벽)는 자동으로 잘린다.
    /// 1인칭이라 내 몸은 평소 그림자 전용 — 반사 카메라가 그리는 동안만 보이게 해서 거울로 스킨을 확인할 수 있다.
    /// </summary>
    [DefaultExecutionOrder(1000)] // PlayerCameraRig(LateUpdate)가 눈 위치를 정한 뒤에 그린다
    public class MirrorReflection : MonoBehaviour
    {
        [SerializeField] private Renderer _glass;           // Quad (로컬 -0.5~0.5, UV 0~1), URP Unlit 머티리얼
        [SerializeField] private int _textureSize = 512;
        [SerializeField] private float _maxDistance = 10f;  // 눈이 이보다 멀면 갱신 안 함 (m)
        [SerializeField] private float _farClip = 30f;
        [SerializeField] private Color _tint = new(0.92f, 0.95f, 1f, 1f);

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Camera _reflectionCam;
        private RenderTexture _texture;
        private MaterialPropertyBlock _block;
        private Renderer[] _ownBody;
        private ShadowCastingMode[] _ownBodyModes;
        private Transform _ownBodyRoot;

        private void Awake()
        {
            _texture = new RenderTexture(_textureSize, _textureSize, 24) { name = "MirrorReflection" };
            var go = new GameObject("MirrorReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            _reflectionCam = go.AddComponent<Camera>();
            _reflectionCam.enabled = false; // 직접 Render()로만 그린다
            _reflectionCam.targetTexture = _texture;
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        private void OnDestroy()
        {
            if (_reflectionCam != null) Destroy(_reflectionCam.gameObject);
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        private void LateUpdate()
        {
            var eyeCam = Camera.main;
            if (eyeCam == null || _glass == null) return;

            Transform g = _glass.transform;
            Vector3 pa = g.TransformPoint(-0.5f, -0.5f, 0f); // UV (0,0)
            Vector3 pb = g.TransformPoint(0.5f, -0.5f, 0f);  // UV (1,0)
            Vector3 pc = g.TransformPoint(-0.5f, 0.5f, 0f);  // UV (0,1)
            Vector3 up = (pc - pa).normalized;
            Vector3 normal = Vector3.Cross(pb - pa, up).normalized;
            Vector3 eye = eyeCam.transform.position;
            float eyeDist = Vector3.Dot(eye - pa, normal);
            if (eyeDist < 0f) { normal = -normal; eyeDist = -eyeDist; } // 보는 쪽(방)을 향하게
            if (eyeDist < 0.01f || eyeDist > _maxDistance) return;
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(eyeCam), _glass.bounds)) return;

            // 뒤집은 눈에서 거울 창을 통해 방을 보면, 그 창에 비치는 그림이 곧 거울상이다
            Vector3 mirrorEye = eye - 2f * eyeDist * normal;
            Quaternion rot = Quaternion.LookRotation(normal, up);
            _reflectionCam.transform.SetPositionAndRotation(mirrorEye, rot);
            Vector3 right = rot * Vector3.right;
            float xa = Vector3.Dot(pa - mirrorEye, right);
            float xb = Vector3.Dot(pb - mirrorEye, right);
            float yb = Vector3.Dot(pa - mirrorEye, up);
            float yt = Vector3.Dot(pc - mirrorEye, up);
            _reflectionCam.projectionMatrix = Matrix4x4.Frustum(Mathf.Min(xa, xb), Mathf.Max(xa, xb), yb, yt, eyeDist, _farClip);
            _reflectionCam.Render();

            // 텍스처 x축(카메라 오른쪽)이 Quad의 u축과 반대면 뒤집어 붙인다
            bool flip = xb < xa;
            _block ??= new MaterialPropertyBlock(); // 도메인 리로드로 비었을 때도
            _glass.GetPropertyBlock(_block);
            _block.SetTexture(BaseMapId, _texture);
            _block.SetVector(BaseMapStId, flip ? new Vector4(-1f, 1f, 1f, 0f) : new Vector4(1f, 1f, 0f, 0f));
            _block.SetColor(BaseColorId, _tint);
            _glass.SetPropertyBlock(_block);
        }

        // 컬링 전에 불린다 — 이 카메라에서만 내 몸을 보이게 했다가 끝나면 되돌린다
        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _reflectionCam) return;
            CacheOwnBody();
            if (_ownBody == null) return;
            for (int i = 0; i < _ownBody.Length; i++)
            {
                if (_ownBody[i] == null) continue;
                _ownBodyModes[i] = _ownBody[i].shadowCastingMode;
                _ownBody[i].shadowCastingMode = ShadowCastingMode.On;
            }
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _reflectionCam || _ownBody == null) return;
            for (int i = 0; i < _ownBody.Length; i++)
                if (_ownBody[i] != null) _ownBody[i].shadowCastingMode = _ownBodyModes[i];
        }

        private void CacheOwnBody()
        {
            var nm = NetworkManager.Singleton;
            // 네트워크가 꺼지는 중엔 GetLocalPlayerObject가 안에서 터진다(기지 → 메뉴, 고양이 263) — 듣는 중일 때만
            var player = nm != null && nm.IsListening && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (player == null) { _ownBody = null; _ownBodyRoot = null; return; }
            if (_ownBodyRoot == player.transform && _ownBody != null) return;
            _ownBodyRoot = player.transform;
            _ownBody = player.GetComponentsInChildren<MeshRenderer>();
            _ownBodyModes = new ShadowCastingMode[_ownBody.Length];
        }
    }
}
