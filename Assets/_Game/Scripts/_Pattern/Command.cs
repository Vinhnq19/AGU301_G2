// Command.cs
// Base abstract class cho Command Pattern.
// Mọi lệnh cụ thể (Concrete Command) kế thừa lớp này và cài đặt Execute/Undo.

/// <summary>
/// Abstract base class của Command Pattern.
/// Định nghĩa hợp đồng Execute() và Undo() cho mọi lệnh cụ thể.
/// </summary>
public abstract class Command
{
    /// <summary>
    /// Thực thi lệnh – phải được override bởi Concrete Command.
    /// </summary>
    public abstract void Execute();

    /// <summary>
    /// Hoàn tác lệnh vừa thực thi – phải được override bởi Concrete Command.
    /// </summary>
    public abstract void Undo();
}
