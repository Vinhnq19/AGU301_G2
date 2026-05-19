// EnemyFSM.cs
// Demo Finite State Machine (FSM) cho Enemy sử dụng Enum + Switch.
// Kế thừa StateMachine<EnemyState> và triển khai logic từng trạng thái.

using UnityEngine;

/// <summary>
/// Các trạng thái có thể có của Enemy FSM.
/// </summary>
public enum EnemyState
{
    Idle,       // Đứng yên, chờ phát hiện Player
    Patrol,     // Tuần tra theo tuyến cố định
    Chase,      // Phát hiện Player, đuổi theo
    Attack,     // Player trong tầm, tấn công
    Dead        // Đã chết, không hoạt động
}

/// <summary>
/// FSM điều khiển hành vi Enemy: Idle → Patrol → Chase → Attack → Dead.
/// Gắn script này lên Enemy Prefab. Điều chỉnh detectionRange và attackRange trong Inspector.
/// </summary>
public class EnemyFSM : StateMachine<EnemyState>
{
    [Header("FSM Settings")]
    // Bán kính phát hiện Player để chuyển sang Chase
    [SerializeField] private float detectionRange = 8f;

    // Bán kính tấn công để chuyển sang Attack
    [SerializeField] private float attackRange = 2f;

    // Thời gian giữa mỗi đòn tấn công
    [SerializeField] private float attackCooldown = 1.5f;

    // Tốc độ di chuyển khi Chase
    [SerializeField] private float chaseSpeed = 3f;

    [SerializeField] private GameObject _player;

    // Đếm thời gian cooldown tấn công
    private float _attackTimer;

    /// <summary>
    /// Tìm Player trong scene và bắt đầu ở trạng thái Idle.
    /// </summary>
    private void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player");

        if (_player == null)
            Debug.LogWarning("[EnemyFSM] Không tìm thấy Player! Hãy đặt tag 'Player' cho Player GameObject.");

        // Gọi OnEnter cho trạng thái khởi đầu
        OnEnter(CurrentState);
    }

    // ──────────────────────────────────────────────
    //  OnEnter – logic khi VÀO một trạng thái
    // ──────────────────────────────────────────────

    /// <summary>
    /// Xử lý logic khi bắt đầu vào một trạng thái mới.
    /// </summary>
    protected override void OnEnter(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                Debug.Log("[EnemyFSM] ENTER Idle – đứng yên.");
                break;

            case EnemyState.Patrol:
                Debug.Log("[EnemyFSM] ENTER Patrol – bắt đầu tuần tra.");
                break;

            case EnemyState.Chase:
                Debug.Log("[EnemyFSM] ENTER Chase – phát hiện Player, đuổi theo!");
                break;

            case EnemyState.Attack:
                _attackTimer = 0f;
                Debug.Log("[EnemyFSM] ENTER Attack – Player trong tầm, tấn công!");
                break;

            case EnemyState.Dead:
                Debug.Log("[EnemyFSM] ENTER Dead – Enemy đã chết.");
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  OnUpdate – logic mỗi frame theo trạng thái
    // ──────────────────────────────────────────────

    /// <summary>
    /// Thực thi logic mỗi frame dựa trên trạng thái hiện tại.
    /// </summary>
    protected override void OnUpdate(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;

            case EnemyState.Patrol:
                UpdatePatrol();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            case EnemyState.Attack:
                UpdateAttack();
                break;

            case EnemyState.Dead:
                // Không làm gì khi đã chết
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  OnExit – logic khi THOÁT khỏi một trạng thái
    // ──────────────────────────────────────────────

    /// <summary>
    /// Xử lý dọn dẹp khi thoát khỏi một trạng thái.
    /// </summary>
    protected override void OnExit(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Chase:
                Debug.Log("[EnemyFSM] EXIT Chase.");
                break;

            case EnemyState.Attack:
                Debug.Log("[EnemyFSM] EXIT Attack.");
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  Logic từng State
    // ──────────────────────────────────────────────

    /// <summary>
    /// Idle: sau 2 giây chuyển sang Patrol.
    /// </summary>
    private float _idleTimer;
    private void UpdateIdle()
    {
        _idleTimer += Time.deltaTime;
        if (_idleTimer >= 2f)
        {
            _idleTimer = 0f;
            ChangeState(EnemyState.Patrol);
        }
    }

    /// <summary>
    /// Patrol: nếu phát hiện Player thì Chase, ngược lại tuần tra (demo chỉ log).
    /// </summary>
    private void UpdatePatrol()
    {
        if (_player == null) return;

        float dist = Vector2.Distance(transform.position, _player.transform.position);
        if (dist <= detectionRange)
            ChangeState(EnemyState.Chase);
    }

    /// <summary>
    /// Chase: di chuyển về phía Player. Nếu đủ gần thì Attack, nếu quá xa thì Patrol.
    /// </summary>
    private void UpdateChase()
    {
        if (_player == null) return;

        float dist = Vector2.Distance(transform.position, _player.transform.position);

        if (dist <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        if (dist > detectionRange * 1.5f)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // Di chuyển về phía Player
        Vector2 dir = ((Vector2)_player.transform.position - (Vector2)transform.position).normalized;
        transform.Translate(dir * chaseSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Attack: tấn công Player theo cooldown. Nếu Player chạy ra ngoài tầm thì Chase lại.
    /// </summary>
    private void UpdateAttack()
    {
        if (_player == null) return;

        float dist = Vector2.Distance(transform.position, _player.transform.position);

        if (dist > attackRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= attackCooldown)
        {
            _attackTimer = 0f;
            Debug.Log("[EnemyFSM] ⚔️ Enemy tấn công Player!");
        }
    }

    /// <summary>
    /// Chuyển Enemy sang trạng thái Dead từ bên ngoài (gọi từ TakeDamage/Die).
    /// </summary>
    public void Die()
    {
        ChangeState(EnemyState.Dead);
    }

    /// <summary>
    /// Hiển thị vùng detection và attack trong Scene View để dễ debug.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
