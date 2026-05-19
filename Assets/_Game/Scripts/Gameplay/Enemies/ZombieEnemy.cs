using UnityEngine;

/// <summary>
/// Enemy loại Zombie: chậm, máu thấp, dễ tiêu diệt nhất.
/// </summary>
public class ZombieEnemy : MonoBehaviour, IEnemy
{   
    // Tên loại enemy
    public string EnemyType => "Zombie";

    // Máu hiện tại
    public float Health { get; private set; }

    // Máu tối đa
    private const float MaxHealth = 50f;

    /// <summary>
    /// Khởi tạo thông số Zombie khi được spawn.
    /// </summary>
    public void Initialize()
    {
        Health = MaxHealth;
        Debug.Log($"[ZombieEnemy] Zombie đã được spawn với {Health} HP.");
    }

    /// <summary>
    /// Xử lý nhận sát thương và kiểm tra chết.
    /// </summary>
    public void TakeDamage(float damage)
    {
        Health -= damage;
        Debug.Log($"[ZombieEnemy] Zombie nhận {damage} sát thương. HP còn lại: {Health}");

        if (Health <= 0)
            Die();
    }

    /// <summary>
    /// Xử lý khi Zombie chết: log và hủy object.
    /// </summary>
    public void Die()
    {
        Debug.Log("[ZombieEnemy] Zombie đã chết!");
        GameManager.Instance.AddScore(10);
        Destroy(gameObject);
    }
}
