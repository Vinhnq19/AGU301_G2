// CameraController.cs
// Hệ thống Camera 2D hoàn chỉnh: Smooth Follow + Map Bound + Camera Shake.
// Mỗi player gắn 1 CameraController riêng — hỗ trợ multi-camera / split-screen.
// Gắn script này lên GameObject chứa Camera component.

using UnityEngine;

/// <summary>
/// CameraController — component điều khiển camera 2D bám theo player.
/// Bao gồm:
///   • Smooth Follow  : bám mượt theo target với tốc độ tuỳ chỉnh
///   • Offset          : camera lệch so với target (ví dụ nhìn phía trước)
///   • Look Ahead      : camera dự đoán theo hướng di chuyển
///   • Map Bound       : giới hạn camera không vượt ra ngoài map
///   • Camera Shake    : rung camera khi bị hit
///
/// Cách dùng:
///   1. Tạo 1 Camera GameObject → gắn script CameraController
///   2. Kéo Player vào trường "target" trên Inspector
///   3. Tuỳ chỉnh các thông số: followSpeed, offset, lookAheadDistance...
///   4. Gọi SetBounds() khi load map mới để cập nhật giới hạn camera
/// </summary>
public class CameraController : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    //  REGION: CAMERA FOLLOW — Logic bám theo player
    // ═══════════════════════════════════════════════
    #region Camera Follow

    [Header("══ Camera Follow ══")]

    [Tooltip("Player (hoặc bất kỳ Transform nào) mà camera sẽ bám theo.")]
    [SerializeField] private Transform target;

    [Tooltip("Tốc độ bám theo — càng CAO camera bám càng NHANH, càng THẤP càng mượt (trôi nhẹ).")]
    [SerializeField] [Range(0.5f, 20f)] private float followSpeed = 5f;

    [Tooltip("Độ lệch cố định so với target (x = sang phải, y = lên trên). " +
             "Ví dụ (2, 1) → camera nhìn lệch phải-trên so với player.")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Tooltip("Khoảng cách camera nhìn trước theo hướng di chuyển. " +
             "0 = không nhìn trước, 3 = camera dự đoán 3 đơn vị phía trước.")]
    [SerializeField] [Range(0f, 10f)] private float lookAheadDistance = 2f;

    [Tooltip("Tốc độ chuyển đổi Look Ahead — càng THẤP look ahead càng mượt.")]
    [SerializeField] [Range(0.5f, 15f)] private float lookAheadSpeed = 3f;

    // Vị trí target ở frame trước — dùng để tính hướng di chuyển
    private Vector3 _previousTargetPos;

    // Giá trị look ahead hiện tại (được lerp mượt, không nhảy đột ngột)
    private Vector2 _currentLookAhead;

    #endregion

    // ═══════════════════════════════════════════════
    //  REGION: CAMERA BOUND — Giới hạn vùng camera
    // ═══════════════════════════════════════════════
    #region Camera Bound

    [Header("══ Camera Bound ══")]

    [Tooltip("BẬT / TẮT giới hạn camera theo biên map.")]
    [SerializeField] private bool useBounds = false;

    [Tooltip("Toạ độ X nhỏ nhất (biên trái map).")]
    [SerializeField] private float minX = -50f;

    [Tooltip("Toạ độ X lớn nhất (biên phải map).")]
    [SerializeField] private float maxX = 50f;

    [Tooltip("Toạ độ Y nhỏ nhất (biên dưới map).")]
    [SerializeField] private float minY = -50f;

    [Tooltip("Toạ độ Y lớn nhất (biên trên map).")]
    [SerializeField] private float maxY = 50f;

    // Nửa kích thước camera theo world unit — dùng để clamp chính xác
    private float _camHalfHeight;
    private float _camHalfWidth;

    #endregion

    // ═══════════════════════════════════════════════
    //  REGION: CAMERA SHAKE — Rung camera
    // ═══════════════════════════════════════════════
    #region Camera Shake

    [Header("══ Camera Shake (Bonus) ══")]

    [Tooltip("Thời gian rung mặc định (giây).")]
    [SerializeField] private float defaultShakeDuration = 0.3f;

    [Tooltip("Cường độ rung mặc định (pixel offset).")]
    [SerializeField] private float defaultShakeIntensity = 0.2f;

    // Bộ đếm thời gian shake còn lại
    private float _shakeTimeRemaining;

    // Cường độ shake hiện tại
    private float _shakeIntensity;

    #endregion

    // ═══════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Khởi tạo: tính kích thước camera, lưu vị trí ban đầu của target.
    /// </summary>
    private void Start()
    {
        CalculateCameraSize();

        if (target != null)
        {
            _previousTargetPos = target.position;

            // Snap camera ngay đến target lần đầu (không lerp)
            Vector3 startPos = CalculateDesiredPosition();
            transform.position = new Vector3(startPos.x, startPos.y, transform.position.z);
        }
    }

    /// <summary>
    /// LateUpdate — chạy SAU tất cả Update() để camera luôn bám đúng vị trí
    /// mới nhất của player, tránh hiện tượng giật (jitter).
    /// </summary>
    private void LateUpdate()
    {
        // Không có target → không làm gì
        if (target == null) return;

        // --- Bước 1: Tính vị trí mong muốn (follow + offset + look ahead) ---
        Vector3 desiredPos = CalculateDesiredPosition();

        // --- Bước 2: Lerp mượt mà từ vị trí hiện tại đến vị trí mong muốn ---
        Vector3 smoothPos = Vector3.Lerp(
            transform.position,
            new Vector3(desiredPos.x, desiredPos.y, transform.position.z),
            followSpeed * Time.deltaTime
        );

        // --- Bước 3: Clamp theo bound (nếu bật) ---
        if (useBounds)
        {
            smoothPos = ClampToBounds(smoothPos);
        }

        // --- Bước 4: Áp dụng camera shake (nếu đang rung) ---
        if (_shakeTimeRemaining > 0f)
        {
            // Tạo offset ngẫu nhiên theo vòng tròn
            Vector2 shakeOffset = Random.insideUnitCircle * _shakeIntensity;
            smoothPos += new Vector3(shakeOffset.x, shakeOffset.y, 0f);
            _shakeTimeRemaining -= Time.deltaTime;
        }

        // --- Bước 5: Gán vị trí cuối cùng cho camera ---
        transform.position = smoothPos;

        // Cập nhật vị trí target cho frame tiếp theo
        _previousTargetPos = target.position;
    }

    // ═══════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Tính vị trí camera mong muốn dựa trên: target + offset + look ahead.
    /// </summary>
    private Vector3 CalculateDesiredPosition()
    {
        // Hướng di chuyển của target (tính từ frame trước)
        Vector2 moveDirection = ((Vector2)(target.position - _previousTargetPos)).normalized;

        // Look ahead: lerp mượt để tránh camera nhảy khi đổi hướng đột ngột
        Vector2 targetLookAhead = moveDirection * lookAheadDistance;
        _currentLookAhead = Vector2.Lerp(
            _currentLookAhead,
            targetLookAhead,
            lookAheadSpeed * Time.deltaTime
        );

        // Vị trí cuối = target + offset cố định + look ahead
        float desiredX = target.position.x + offset.x + _currentLookAhead.x;
        float desiredY = target.position.y + offset.y + _currentLookAhead.y;

        return new Vector3(desiredX, desiredY, transform.position.z);
    }

    /// <summary>
    /// Clamp vị trí camera sao cho viewport không vượt ra ngoài biên map.
    /// Sử dụng nửa kích thước camera (half width/height) để tính toán chính xác.
    /// </summary>
    private Vector3 ClampToBounds(Vector3 position)
    {
        // Cập nhật kích thước camera (phòng trường hợp thay đổi size runtime)
        CalculateCameraSize();

        // Clamp X: camera.center.x phải nằm trong [minX + halfW, maxX - halfW]
        float clampedX = Mathf.Clamp(position.x, minX + _camHalfWidth, maxX - _camHalfWidth);

        // Clamp Y: camera.center.y phải nằm trong [minY + halfH, maxY - halfH]
        float clampedY = Mathf.Clamp(position.y, minY + _camHalfHeight, maxY - _camHalfHeight);

        return new Vector3(clampedX, clampedY, position.z);
    }

    /// <summary>
    /// Tính nửa kích thước camera (world unit) dựa trên orthographicSize.
    /// Camera 2D (orthographic): height = 2 * orthographicSize,
    ///                           width  = height * aspect.
    /// </summary>
    private void CalculateCameraSize()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null && cam.orthographic)
        {
            _camHalfHeight = cam.orthographicSize;
            _camHalfWidth = _camHalfHeight * cam.aspect;
        }
    }

    // ═══════════════════════════════════════════════
    //  PUBLIC API — Các hàm tiện ích gọi từ bên ngoài
    // ═══════════════════════════════════════════════

    #region Public API

    /// <summary>
    /// Gán target (player) mới cho camera. Có thể gọi runtime để đổi target.
    /// Ví dụ: cameraController.SetTarget(player2.transform);
    /// </summary>
    /// <param name="newTarget">Transform của player/object cần bám theo</param>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            // Reset look ahead khi đổi target để tránh camera nhảy
            _previousTargetPos = target.position;
            _currentLookAhead = Vector2.zero;
        }

        Debug.Log($"[CameraController] Target changed → {(newTarget != null ? newTarget.name : "null")}");
    }

    /// <summary>
    /// Đặt giới hạn biên map cho camera. Gọi khi load map mới.
    /// Tự động bật useBounds = true.
    /// Ví dụ: cameraController.SetBounds(-20f, 80f, -10f, 40f);
    /// </summary>
    /// <param name="newMinX">Biên trái map (world X nhỏ nhất)</param>
    /// <param name="newMaxX">Biên phải map (world X lớn nhất)</param>
    /// <param name="newMinY">Biên dưới map (world Y nhỏ nhất)</param>
    /// <param name="newMaxY">Biên trên map (world Y lớn nhất)</param>
    public void SetBounds(float newMinX, float newMaxX, float newMinY, float newMaxY)
    {
        minX = newMinX;
        maxX = newMaxX;
        minY = newMinY;
        maxY = newMaxY;
        useBounds = true;

        Debug.Log($"[CameraController] Bounds updated → X[{minX}, {maxX}] Y[{minY}, {maxY}]");
    }

    /// <summary>
    /// Đặt giới hạn biên map bằng Rect (tiện khi lấy từ Tilemap.localBounds).
    /// Ví dụ: cameraController.SetBounds(tilemap.localBounds);
    /// </summary>
    /// <param name="boundsRect">Rect chứa thông tin biên map</param>
    public void SetBounds(Rect boundsRect)
    {
        SetBounds(boundsRect.xMin, boundsRect.xMax, boundsRect.yMin, boundsRect.yMax);
    }

    /// <summary>
    /// Đặt giới hạn biên map bằng Bounds (từ Collider/Tilemap).
    /// Ví dụ: cameraController.SetBounds(tilemapRenderer.bounds);
    /// </summary>
    /// <param name="bounds">Bounds chứa thông tin biên map</param>
    public void SetBounds(Bounds bounds)
    {
        SetBounds(bounds.min.x, bounds.max.x, bounds.min.y, bounds.max.y);
    }

    /// <summary>
    /// Tắt giới hạn biên — camera tự do bám theo target.
    /// </summary>
    public void DisableBounds()
    {
        useBounds = false;
        Debug.Log("[CameraController] Bounds disabled.");
    }

    /// <summary>
    /// Thay đổi tốc độ follow runtime.
    /// Giá trị thấp (1-3) → camera trôi chậm, mượt.
    /// Giá trị cao (8-15) → camera bám sát, phản hồi nhanh.
    /// </summary>
    /// <param name="speed">Tốc độ follow mới</param>
    public void SetFollowSpeed(float speed)
    {
        followSpeed = Mathf.Max(0.1f, speed); // Không cho phép <= 0
        Debug.Log($"[CameraController] Follow speed → {followSpeed}");
    }

    /// <summary>
    /// Thay đổi offset runtime. Ví dụ: khi player nhắm bắn, dịch camera về phía đó.
    /// </summary>
    /// <param name="newOffset">Offset mới (x, y)</param>
    public void SetOffset(Vector2 newOffset)
    {
        offset = newOffset;
    }

    /// <summary>
    /// Thay đổi khoảng look ahead runtime.
    /// </summary>
    /// <param name="distance">Khoảng cách look ahead mới</param>
    public void SetLookAheadDistance(float distance)
    {
        lookAheadDistance = Mathf.Max(0f, distance);
    }

    /// <summary>
    /// Rung camera — gọi khi player bị hit, nổ, v.v.
    /// Sử dụng giá trị mặc định (defaultShakeDuration, defaultShakeIntensity).
    /// </summary>
    public void ShakeCamera()
    {
        ShakeCamera(defaultShakeDuration, defaultShakeIntensity);
    }

    /// <summary>
    /// Rung camera với thời gian và cường độ tuỳ chỉnh.
    /// Ví dụ: cameraController.ShakeCamera(0.5f, 0.3f);
    /// </summary>
    /// <param name="duration">Thời gian rung (giây)</param>
    /// <param name="intensity">Cường độ rung (đơn vị world — 0.1~0.5 là vừa đủ)</param>
    public void ShakeCamera(float duration, float intensity)
    {
        _shakeTimeRemaining = duration;
        _shakeIntensity = intensity;
    }

    #endregion

    // ═══════════════════════════════════════════════
    //  GIZMOS — Vẽ giới hạn bound trong Scene View
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Vẽ hình chữ nhật biểu thị vùng bound trong Scene View để dễ debug.
    /// Chỉ hiển thị khi useBounds = true và chọn camera trong Hierarchy.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f); // Xanh lá bán trong suốt

        // Tính center và size của vùng bound
        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0f);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0f);

        Gizmos.DrawWireCube(center, size);
    }
}