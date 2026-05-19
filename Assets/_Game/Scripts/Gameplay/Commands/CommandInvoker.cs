// CommandInvoker.cs
// Bộ thực thi Command Pattern: quản lý hàng đợi lệnh và hỗ trợ Undo/Redo.
// Gắn script này lên Player hoặc GameManager để điều khiển toàn bộ chuỗi lệnh.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý và thực thi các Command, hỗ trợ Undo lệnh gần nhất.
/// Gắn lên một GameObject trong scene để sử dụng.
/// </summary>
public class CommandInvoker : MonoBehaviour
{
    // Stack lưu lịch sử các lệnh đã thực thi để hỗ trợ Undo
    private readonly Stack<Command> _history = new Stack<Command>();

    /// <summary>
    /// Thực thi một lệnh và đẩy vào lịch sử để có thể Undo sau.
    /// </summary>
    /// <param name="command">Lệnh cần thực thi</param>
    public void ExecuteCommand(Command command)
    {
        command.Execute();
        _history.Push(command);
    }

    /// <summary>
    /// Hoàn tác lệnh gần nhất đã thực thi.
    /// Không làm gì nếu lịch sử trống.
    /// </summary>
    public void UndoLastCommand()
    {
        if (_history.Count == 0)
        {
            Debug.Log("[CommandInvoker] Không có lệnh nào để Undo.");
            return;
        }

        Command lastCommand = _history.Pop();
        lastCommand.Undo();
    }

    /// <summary>
    /// Xóa toàn bộ lịch sử lệnh (ví dụ khi bắt đầu game mới).
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        Debug.Log("[CommandInvoker] Đã xóa toàn bộ lịch sử lệnh.");
    }

    /// <summary>
    /// Demo: nhấn H để Heal, U để Undo lệnh cuối.
    /// Xóa block này khi tích hợp vào game thật.
    /// </summary>
    [Header("Demo References")]
    [SerializeField] private PlayerStats playerStats;

    private void Update()
    {
        // Nhấn H: thực thi HealCommand
        if (Input.GetKeyDown(KeyCode.H) && playerStats != null)
            ExecuteCommand(new HealCommand(playerStats, 20f));

        // Nhấn U: Undo lệnh gần nhất
        if (Input.GetKeyDown(KeyCode.U))
            UndoLastCommand();
    }
}
