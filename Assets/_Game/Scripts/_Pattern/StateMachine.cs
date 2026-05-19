// StateMachine.cs
// Generic Finite State Machine (FSM) sử dụng Enum để định nghĩa trạng thái.
// Concrete FSM kế thừa lớp này, định nghĩa Enum riêng và override các handler.

using UnityEngine;

/// <summary>
/// Generic FSM base class dùng Enum làm kiểu trạng thái.
/// Kế thừa lớp này, truyền Enum của bạn vào T, rồi override OnEnter/OnUpdate/OnExit.
/// </summary>
public abstract class StateMachine<T> : MonoBehaviour where T : System.Enum
{
    // Trạng thái đang hoạt động hiện tại
    protected T CurrentState { get; private set; }

    /// <summary>
    /// Chuyển sang trạng thái mới: gọi OnExit trạng thái cũ → gán → gọi OnEnter trạng thái mới.
    /// Bỏ qua nếu trạng thái mới giống trạng thái hiện tại.
    /// </summary>
    /// <param name="newState">Trạng thái muốn chuyển sang</param>
    protected void ChangeState(T newState)
    {
        if (CurrentState.Equals(newState)) return;

        OnExit(CurrentState);
        Debug.Log($"[FSM] {GetType().Name}: {CurrentState} → {newState}");
        CurrentState = newState;
        OnEnter(CurrentState);
    }

    /// <summary>
    /// Gọi mỗi frame – thực thi logic của trạng thái hiện tại.
    /// Override để xử lý Update theo từng state (thường dùng switch).
    /// </summary>
    protected abstract void OnUpdate(T state);

    /// <summary>
    /// Gọi khi bắt đầu vào một trạng thái mới.
    /// Override để xử lý logic khởi tạo state (animation, reset timer,...).
    /// </summary>
    protected abstract void OnEnter(T state);

    /// <summary>
    /// Gọi khi thoát khỏi một trạng thái.
    /// Override để dọn dẹp trước khi chuyển state (dừng animation,...).
    /// </summary>
    protected abstract void OnExit(T state);

    /// <summary>
    /// Chạy OnUpdate mỗi frame với trạng thái hiện tại.
    /// </summary>
    private void Update()
    {
        OnUpdate(CurrentState);
    }
}
