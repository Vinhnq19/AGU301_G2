public interface IEnemy
{
    string EnemyType { get; }
    float Health { get; }
    void Initialize();
    void TakeDamage(float damage);
    void Die();
}
