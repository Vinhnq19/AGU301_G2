// PlayerFSM.cs
// Demo Finite State Machine (FSM) cho Player sử dụng Enum + Switch.
// Kế thừa StateMachine<PlayerState> – minh họa tái sử dụng pattern.

using UnityEngine;

/// <summary>
/// Các trạng thái có thể có của Player FSM.
/// </summary>
public enum PlayerState
{
    Idle,       // Đứng yên, chờ input
    Running,    // Đang di chuyển (WASD)
    Attacking,  // Đang tấn công (Space / Click)
    Dead        // Đã chết
}

/// <summary>
/// FSM điều khiển hành vi Player: Idle ↔ Running ↔ Attacking → Dead.
/// Gắn script này lên Player GameObject trong scene.
/// </summary>
public class PlayerFSM : StateMachine<PlayerState>
{
    [Header("FSM Settings")]
    // Ngưỡng tốc độ để chuyển sang Running
    [SerializeField] private float runThreshold = 0.1f;

    // Thời gian animation tấn công (giây) trước khi về Idle
    [SerializeField] private float attackDuration = 0.8f;

    // Đếm thời gian trạng thái Attack
    private float _attackTimer;

    // Đếm thời gian trạng thái Idle
    private float _idleTimer;

    /// <summary>
    /// Khởi tạo trạng thái ban đầu là Idle.
    /// </summary>
    private void Start()
    {
        OnEnter(CurrentState);
    }

    // ──────────────────────────────────────────────
    //  OnEnter – logic khi VÀO một trạng thái
    // ──────────────────────────────────────────────

    protected override void OnEnter(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Idle:
                Debug.Log("[PlayerFSM] ENTER Idle – đứng yên, chờ input.");
                break;

            case PlayerState.Running:
                Debug.Log("[PlayerFSM] ENTER Running – bắt đầu di chuyển.");
                break;

            case PlayerState.Attacking:
                _attackTimer = 0f;
                Debug.Log("[PlayerFSM] ENTER Attacking – thực hiện đòn tấn công!");
                break;

            case PlayerState.Dead:
                Debug.Log("[PlayerFSM] ENTER Dead – Player đã chết.");
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  OnUpdate – logic mỗi frame theo trạng thái
    // ──────────────────────────────────────────────

    protected override void OnUpdate(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Idle:
                UpdateIdle();
                break;

            case PlayerState.Running:
                UpdateRunning();
                break;

            case PlayerState.Attacking:
                UpdateAttacking();
                break;

            case PlayerState.Dead:
                // Không làm gì khi đã chết
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  OnExit – logic khi THOÁT khỏi một trạng thái
    // ──────────────────────────────────────────────

    protected override void OnExit(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Running:
                Debug.Log("[PlayerFSM] EXIT Running.");
                break;

            case PlayerState.Attacking:
                Debug.Log("[PlayerFSM] EXIT Attacking.");
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  Logic từng State
    // ──────────────────────────────────────────────

    /// <summary>
    /// Idle: nhận WASD → Running, nhận Space/Click → Attacking.
    /// </summary>
    private void UpdateIdle()
    {
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        if (input.magnitude > runThreshold)
        {
            ChangeState(PlayerState.Running);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            ChangeState(PlayerState.Attacking);
    }

    /// <summary>
    /// Running: tiếp tục di chuyển. Dừng input → Idle. Space/Click → Attacking.
    /// </summary>
    private void UpdateRunning()
    {
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            ChangeState(PlayerState.Attacking);
            return;
        }

        if (input.magnitude <= runThreshold)
            ChangeState(PlayerState.Idle);
    }

    /// <summary>
    /// Attacking: chờ hết attackDuration giây rồi về Idle.
    /// </summary>
    private void UpdateAttacking()
    {
        _attackTimer += Time.deltaTime;
        if (_attackTimer >= attackDuration)
            ChangeState(PlayerState.Idle);
    }
}
