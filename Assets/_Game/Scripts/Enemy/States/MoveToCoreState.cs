using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    public sealed class MoveToCoreState : IEnemyState
    {
        public void Enter(BaseEnemy enemy)
        {
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            if (enemy.IsCoreInAttackRange())
            {
                enemy.ChangeState(new AttackCoreState());
                return;
            }

            if (enemy.IsBlockedByWall())
            {
                enemy.ChangeState(new AttackWallState());
                return;
            }

            enemy.MoveTowardsCore();
        }
    }
}
