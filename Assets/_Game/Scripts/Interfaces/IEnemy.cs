/// <summary>
/// Interface định nghĩa hành vi cơ bản của mọi loại Enemy.
/// </summary>
public interface IEnemy
{
    /// <summary>Tên loại enemy (Zombie, Skeleton, Boss,...)</summary>
    string EnemyType { get; }

    /// <summary>Máu hiện tại của enemy</summary>
    float Health { get; }

    /// <summary>
    /// Khởi tạo enemy với các thông số mặc định.
    /// Gọi ngay sau khi enemy được tạo ra từ Factory.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Xử lý khi enemy nhận sát thương.
    /// </summary>
    /// <param name="damage">Lượng sát thương nhận vào</param>
    void TakeDamage(float damage);

    /// <summary>
    /// Xử lý khi enemy chết (HP <= 0).
    /// </summary>
    void Die();
}
