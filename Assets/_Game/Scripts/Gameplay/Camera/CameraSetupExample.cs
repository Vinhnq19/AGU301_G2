// CameraSetupExample.cs
// Script ví dụ minh họa cách setup CameraController trong code.
// KHÔNG cần giữ file này trong game cuối — chỉ để tham khảo & test.
// Gắn script này lên 1 GameObject trống trong scene để chạy demo.

using UnityEngine;

/// <summary>
/// Ví dụ minh họa cách:
///   1. Gán target (player) cho camera
///   2. Set bounds khi load map mới
///   3. Setup split-screen cho 2 player
///   4. Trigger camera shake
/// </summary>
public class CameraSetupExample : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera Controller của Player 1")]
    [SerializeField] private CameraController cam1;

    [Tooltip("Camera Controller của Player 2 (nếu 2 người chơi)")]
    [SerializeField] private CameraController cam2;

    [Tooltip("Transform của Player 1")]
    [SerializeField] private Transform player1;

    [Tooltip("Transform của Player 2")]
    [SerializeField] private Transform player2;

    private void Start()
    {
        // ──────────────────────────────────────────
        //  VÍ DỤ 1: Gán target cho camera
        // ──────────────────────────────────────────
        if (cam1 != null && player1 != null)
        {
            cam1.SetTarget(player1);
        }

        if (cam2 != null && player2 != null)
        {
            cam2.SetTarget(player2);
        }

        // ──────────────────────────────────────────
        //  VÍ DỤ 2: Set bounds khi load map mới
        //  Thay các giá trị bằng kích thước map thật
        // ──────────────────────────────────────────
        SetupMapBounds();

        // ──────────────────────────────────────────
        //  VÍ DỤ 3: Đổi follow speed runtime
        // ──────────────────────────────────────────
        if (cam1 != null)
        {
            cam1.SetFollowSpeed(8f);  // Camera bám nhanh hơn
        }
    }

    /// <summary>
    /// Ví dụ: Set bounds cho camera khi load map.
    /// Trong thực tế, các giá trị này nên lấy từ Tilemap hoặc map data.
    /// </summary>
    private void SetupMapBounds()
    {
        // Cách 1: Truyền 4 giá trị float
        if (cam1 != null)
        {
            cam1.SetBounds(
                -20f,   // minX — biên trái map
                 80f,   // maxX — biên phải map
                -10f,   // minY — biên dưới map
                 40f    // maxY — biên trên map
            );
        }

        // Cách 2: Truyền Rect (tiện khi có sẵn Rect data)
        // cam1.SetBounds(new Rect(-20f, -10f, 100f, 50f));

        // Cách 3: Lấy bounds từ Tilemap (phổ biến nhất)
        // Tilemap tilemap = FindObjectOfType<Tilemap>();
        // tilemap.CompressBounds();
        // cam1.SetBounds(tilemap.localBounds);

        // Áp dụng bounds cho cả cam2 nếu cùng map
        if (cam2 != null)
        {
            cam2.SetBounds(-20f, 80f, -10f, 40f);
        }
    }

    private void Update()
    {
        // ──────────────────────────────────────────
        //  VÍ DỤ 4: Camera shake khi nhấn phím T (test)
        // ──────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.T))
        {
            // Shake với giá trị mặc định
            cam1?.ShakeCamera();

            // Hoặc shake với giá trị tuỳ chỉnh
            // cam1?.ShakeCamera(0.5f, 0.4f);
        }

        // ──────────────────────────────────────────
        //  VÍ DỤ 5: Đổi target runtime (nhấn phím R)
        // ──────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Swap: cam1 bám player2, cam2 bám player1
            cam1?.SetTarget(player2);
            cam2?.SetTarget(player1);
            Debug.Log("[Example] Swapped camera targets!");
        }
    }
}