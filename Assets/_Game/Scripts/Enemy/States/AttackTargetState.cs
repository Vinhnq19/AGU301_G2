using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    /// <summary>
    /// Giao tranh với mục tiêu đã chọn theo bậc ưu tiên (player / tower / core).
    /// Gộp cả "đuổi" và "đánh": chưa tới tầm thì tiến lại, tới tầm thì đánh — nhờ vậy
    /// không cần thêm ChaseState riêng.
    ///
    /// Thay cho AttackCoreState cũ (tên gây hiểu sai: nó vốn đánh cả player lẫn core).
    /// Stateless → dùng instance dùng chung.
    /// </summary>
    public sealed class AttackTargetState : IEnemyState
    {
        public static readonly AttackTargetState Instance = new();

        public void Enter(BaseEnemy enemy)
        {
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            enemy.SenseIfDue();

            if (!enemy.HasAttackTarget)
            {
                enemy.ChangeState(MoveToCoreState.Instance);
                return;
            }

            // Mục tiêu đã phát hiện nhưng còn ngoài tầm đánh → đuổi tới (nếu rule cho phép chase).
            if (!enemy.IsTargetInAttackRange())
            {
                enemy.MoveTowardsTarget();
                return;
            }

            enemy.AttackCurrentTarget();
        }
    }
}
