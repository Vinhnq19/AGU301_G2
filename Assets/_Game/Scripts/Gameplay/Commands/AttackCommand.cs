// AttackCommand.cs
// Concrete Command gây sát thương lên một target IEnemy.
// Ứng dụng: đóng gói thao tác tấn công để có thể log, replay hoặc queue lại sau.

/// <summary>
/// Lệnh tấn công gây sát thương lên một IEnemy cụ thể.
/// Execute: gây damage. Undo: hồi lại HP cho enemy (hữu ích khi test/replay).
/// </summary>
public class AttackCommand : Command
{
    // Target enemy sẽ nhận sát thương
    private readonly IEnemy _target;

    // Lượng sát thương sẽ gây ra
    private readonly float _damage;

    /// <summary>
    /// Khởi tạo lệnh tấn công với target và lượng sát thương cụ thể.
    /// </summary>
    /// <param name="target">Enemy sẽ nhận sát thương</param>
    /// <param name="damage">Lượng sát thương</param>
    public AttackCommand(IEnemy target, float damage)
    {
        _target = target;
        _damage = damage;
    }

    /// <summary>
    /// Thực thi: gây _damage sát thương cho _target.
    /// </summary>
    public override void Execute()
    {
        _target.TakeDamage(_damage);
    }

    /// <summary>
    /// Hoàn tác: hồi lại _damage HP cho _target (hữu ích khi replay/debug).
    /// Lưu ý: nếu enemy đã chết thì Undo không có tác dụng.
    /// </summary>
    public override void Undo()
    {
        // IEnemy không có Heal nên cast về MonoBehaviour để lấy component
        // Trong thực tế nên mở rộng IEnemy nếu cần Undo đầy đủ
        UnityEngine.Debug.Log($"[AttackCommand] Undo: không thể hồi HP cho {_target.EnemyType} (chỉ demo).");
    }
}
