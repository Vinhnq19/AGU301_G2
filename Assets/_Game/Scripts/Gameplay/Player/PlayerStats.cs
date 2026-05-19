// PlayerStats.cs
// Lưu trữ và quản lý các chỉ số cơ bản của Player (HP, Mana).
// Được các Command (HealCommand, AttackCommand) sử dụng để thay đổi trạng thái.

using UnityEngine;

/// <summary>
/// Quản lý chỉ số HP và Mana của Player.
/// Gắn script này lên Player GameObject.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Stats")]
    // Máu tối đa của Player
    [SerializeField] private float maxHealth = 100f;

    // Mana tối đa của Player
    [SerializeField] private float maxMana = 50f;

    // Máu hiện tại
    public float Health { get; private set; }

    // Mana hiện tại
    public float Mana { get; private set; }

    /// <summary>
    /// Khởi tạo chỉ số Player về giá trị tối đa khi bắt đầu.
    /// </summary>
    private void Awake()
    {
        Health = maxHealth;
        Mana = maxMana;
    }

    /// <summary>
    /// Hồi phục một lượng máu, không vượt quá maxHealth.
    /// </summary>
    /// <param name="amount">Lượng HP cần hồi</param>
    public void Heal(float amount)
    {
        Health = Mathf.Min(Health + amount, maxHealth);
        Debug.Log($"[PlayerStats] Hồi {amount} HP. HP hiện tại: {Health}/{maxHealth}");
    }

    /// <summary>
    /// Gây sát thương cho Player, không xuống dưới 0.
    /// </summary>
    /// <param name="amount">Lượng sát thương nhận vào</param>
    public void TakeDamage(float amount)
    {
        Health = Mathf.Max(Health - amount, 0f);
        Debug.Log($"[PlayerStats] Nhận {amount} sát thương. HP hiện tại: {Health}/{maxHealth}");
    }

    /// <summary>
    /// Tiêu hao Mana để dùng kỹ năng. Trả về true nếu đủ Mana.
    /// </summary>
    /// <param name="cost">Lượng Mana cần tiêu</param>
    public bool UseMana(float cost)
    {
        if (Mana < cost)
        {
            Debug.Log($"[PlayerStats] Không đủ Mana! Cần {cost}, hiện có {Mana}.");
            return false;
        }
        Mana -= cost;
        Debug.Log($"[PlayerStats] Tiêu {cost} Mana. Mana hiện tại: {Mana}/{maxMana}");
        return true;
    }

    /// <summary>
    /// Hồi phục Mana, không vượt quá maxMana.
    /// </summary>
    /// <param name="amount">Lượng Mana cần hồi</param>
    public void RestoreMana(float amount)
    {
        Mana = Mathf.Min(Mana + amount, maxMana);
    }
}
