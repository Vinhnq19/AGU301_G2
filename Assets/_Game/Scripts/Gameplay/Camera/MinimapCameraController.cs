using UnityEngine;

namespace DungeonBuilder.Gameplay.Camera
{
    /// <summary>
    /// Gắn vào Camera dùng cho Minimap.
    /// Tự động thiết lập các cấu hình cần thiết để render đúng layer.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class MinimapCameraController : MonoBehaviour
    {
        [SerializeField] private float _orthographicSize = 15f;


        [Tooltip("Layer của Map (thường là Default) và layer Minimap chứa Icon")]
        [SerializeField] private LayerMask _cullingMask;

        private UnityEngine.Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            SetupCamera();
        }

        private void SetupCamera()
        {
            if (_cam == null) return;

            _cam.orthographic = true;
            _cam.orthographicSize = _orthographicSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;

            // Màu nền của Minimap (ví dụ màu tối để dễ nhìn map)

            _cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Tự động đưa Camera lùi lại phía sau (rất quan trọng trong 2D) để có thể nhìn thấy Map
            Vector3 pos = transform.position;
            pos.z = -50f;
            transform.position = pos;

            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 1000f;
        }
    }
}
