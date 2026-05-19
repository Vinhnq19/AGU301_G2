using UnityEngine;

/// <summary>
/// Enemy loại Skeleton: tốc độ trung bình, máu trung bình.
/// </summary>
public class SkeletonEnemy : MonoBehaviour, IEnemy
{
    // Tên loại enemy
    public string EnemyType => "Skeleton";

    // Máu hiện tại
    public float Health { get; private set; }

    // Máu tối đa
    private const float MaxHealth = 80f;

    /// <summary>
    /// Khởi tạo thông số Skeleton khi được spawn.
    /// </summary>
    public void Initialize()
    {
        Health = MaxHealth;
        Debug.Log($"[SkeletonEnemy] Skeleton đã được spawn với {Health} HP.");
    }

    /// <summary>
    /// Xử lý nhận sát thương và kiểm tra chết.
    /// </summary>
    public void TakeDamage(float damage)
    {
        Health -= damage;
        Debug.Log($"[SkeletonEnemy] Skeleton nhận {damage} sát thương. HP còn lại: {Health}");

        if (Health <= 0)
            Die();
    }

    /// <summary>
    /// Xử lý khi Skeleton chết: log và hủy object.
    /// </summary>
    public void Die()
    {
        Debug.Log("[SkeletonEnemy] Skeleton đã chết!");
        GameManager.Instance.AddScore(20);
        Destroy(gameObject);
    }
}
