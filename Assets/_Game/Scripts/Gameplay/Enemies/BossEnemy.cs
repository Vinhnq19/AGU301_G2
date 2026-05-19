
using UnityEngine;

/// <summary>
/// Enemy loại Boss: máu rất cao, xuất hiện cuối mỗi wave.
/// </summary>
public class BossEnemy : MonoBehaviour, IEnemy
{
    // Tên loại enemy
    public string EnemyType => "Boss";

    // Máu hiện tại
    public float Health { get; private set; }

    // Máu tối đa
    private const float MaxHealth = 300f;

    /// <summary>
    /// Khởi tạo thông số Boss khi được spawn.
    /// </summary>
    public void Initialize()
    {
        Health = MaxHealth;
        Debug.Log($"[BossEnemy] *** BOSS ĐÃ XUẤT HIỆN *** với {Health} HP!");
    }

    /// <summary>
    /// Xử lý nhận sát thương và kiểm tra chết.
    /// </summary>
    public void TakeDamage(float damage)
    {
        Health -= damage;
        Debug.Log($"[BossEnemy] Boss nhận {damage} sát thương. HP còn lại: {Health}");

        if (Health <= 0)
            Die();
    }

    /// <summary>
    /// Xử lý khi Boss chết: log, thêm điểm lớn và thông báo thắng wave.
    /// </summary>
    public void Die()
    {
        Debug.Log("[BossEnemy] *** BOSS ĐÃ BỊ TIÊU DIỆT! ***");
        GameManager.Instance.AddScore(100);
        GameManager.Instance.NextWave();
        Destroy(gameObject);
    }
}
