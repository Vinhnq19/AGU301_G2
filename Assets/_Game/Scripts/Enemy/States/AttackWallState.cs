using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    /// <summary>Đánh công trình đang chắn đường. Stateless → dùng instance dùng chung.</summary>
    public sealed class AttackWallState : IEnemyState
    {
        public static readonly AttackWallState Instance = new();

        public void Enter(BaseEnemy enemy)
        {
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            enemy.SenseIfDue();

            // Mục tiêu theo bậc ưu tiên (vd player lại gần) được ưu tiên hơn cái tường chắn đường.
            if (enemy.HasAttackTarget)
            {
                enemy.ChangeState(AttackTargetState.Instance);
                return;
            }

            if (!enemy.IsBlockedByWall())
            {
                enemy.ChangeState(MoveToCoreState.Instance);
                return;
            }

            enemy.AttackCurrentBlocker();
        }
    }
}
