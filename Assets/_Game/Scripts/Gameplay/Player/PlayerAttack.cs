// PlayerAttack.cs
// Script cho phép người chơi tấn công và tiêu diệt enemy gần nhất.
// Sử dụng Physics2D (2D game) – nhấn Space hoặc click chuột trái để tấn công.

using UnityEngine;

/// <summary>
/// Demo tấn công 2D: gây sát thương cho tất cả enemy trong bán kính AttackRange.
/// Gắn script này lên Player GameObject trong scene.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    // Sát thương mỗi đòn tấn công
    [SerializeField] private float attackDamage = 30f;

    // Bán kính phát hiện và tấn công enemy xung quanh (2D)
    [SerializeField] private float attackRange = 3f;

    // Layer chứa các enemy để OverlapCircle lọc đúng đối tượng
    [SerializeField] private LayerMask enemyLayer;

    /// <summary>
    /// Kiểm tra input mỗi frame: Space hoặc click chuột trái để tấn công.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            Attack();
    }

    /// <summary>
    /// Tìm tất cả Collider2D của enemy trong bán kính và gây sát thương.
    /// Dùng Physics2D.OverlapCircleAll vì đây là game 2D (Rigidbody2D / Collider2D).
    /// </summary>
    private void Attack()
    {
        // OverlapCircleAll tìm mọi Collider2D trong vùng tròn theo enemyLayer
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);

        if (hits.Length == 0)
        {
            Debug.Log("[PlayerAttack] Không có enemy nào trong tầm tấn công.");
            return;
        }

        Debug.Log($"[PlayerAttack] Tấn công! Tìm thấy {hits.Length} enemy trong tầm.");

        foreach (Collider2D hit in hits)
        {
            // Lấy component IEnemy từ GameObject bị trúng
            IEnemy enemy = hit.GetComponent<IEnemy>();

            if (enemy != null)
            {
                Debug.Log($"[PlayerAttack] Gây {attackDamage} sát thương cho {enemy.EnemyType}.");
                enemy.TakeDamage(attackDamage);
            }
        }
    }

    /// <summary>
    /// Hiển thị vùng tấn công trong Scene View để dễ debug và căn chỉnh.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Vẽ vòng tròn 2D bằng cách dùng DrawWireSphere với z=0
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
