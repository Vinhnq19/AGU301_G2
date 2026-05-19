// HealCommand.cs
// Concrete Command hồi máu cho Player và hoàn tác bằng cách trừ lại lượng HP đã hồi.
// Ứng dụng: gắn vào nút dùng item hồi máu, có thể Undo nếu nhấn nhầm.

/// <summary>
/// Lệnh hồi máu cho Player.
/// Execute: hồi healAmount HP. Undo: trừ lại lượng HP vừa hồi.
/// </summary>
public class HealCommand : Command
{
    // Tham chiếu đến chỉ số Player cần hồi máu
    private readonly PlayerStats _player;

    // Lượng HP sẽ hồi khi Execute
    private readonly float _healAmount;

    /// <summary>
    /// Khởi tạo lệnh hồi máu với target và lượng hồi cụ thể.
    /// </summary>
    /// <param name="player">PlayerStats cần hồi máu</param>
    /// <param name="healAmount">Lượng HP sẽ hồi</param>
    public HealCommand(PlayerStats player, float healAmount)
    {
        _player = player;
        _healAmount = healAmount;
    }

    /// <summary>
    /// Thực thi: hồi _healAmount HP cho Player.
    /// </summary>
    public override void Execute()
    {
        _player.Heal(_healAmount);
    }

    /// <summary>
    /// Hoàn tác: trừ lại _healAmount HP (mô phỏng undo hồi máu).
    /// </summary>
    public override void Undo()
    {
        _player.TakeDamage(_healAmount);
    }
}
