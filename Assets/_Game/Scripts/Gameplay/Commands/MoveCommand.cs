// MoveCommand.cs
// Concrete Command di chuyển một Transform đến vị trí mới.
// Ứng dụng: queue các bước di chuyển, hỗ trợ Undo để quay lại vị trí cũ.

using UnityEngine;

/// <summary>
/// Lệnh di chuyển một Transform đến vị trí đích.
/// Execute: dịch chuyển đến vị trí mới. Undo: quay về vị trí cũ.
/// </summary>
public class MoveCommand : Command
{
    // Transform cần di chuyển
    private readonly Transform _transform;

    // Vị trí đích cần di chuyển đến
    private readonly Vector3 _targetPosition;

    // Lưu vị trí trước khi di chuyển để Undo
    private Vector3 _previousPosition;

    /// <summary>
    /// Khởi tạo lệnh di chuyển với target Transform và vị trí đích.
    /// </summary>
    /// <param name="transform">Transform sẽ được di chuyển</param>
    /// <param name="targetPosition">Vị trí đích trong world space</param>
    public MoveCommand(Transform transform, Vector3 targetPosition)
    {
        _transform = transform;
        _targetPosition = targetPosition;
    }

    /// <summary>
    /// Lưu vị trí hiện tại rồi di chuyển Transform đến vị trí đích.
    /// </summary>
    public override void Execute()
    {
        _previousPosition = _transform.position;
        _transform.position = _targetPosition;
        Debug.Log($"[MoveCommand] Di chuyển từ {_previousPosition} → {_targetPosition}");
    }

    /// <summary>
    /// Quay Transform về vị trí trước khi Execute.
    /// </summary>
    public override void Undo()
    {
        _transform.position = _previousPosition;
        Debug.Log($"[MoveCommand] Undo: quay về {_previousPosition}");
    }
}
