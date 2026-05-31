using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    public sealed class AttackCoreState : IEnemyState
    {
        public void Enter(BaseEnemy enemy)
        {
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            if (!enemy.IsCoreInAttackRange())
            {
                enemy.ChangeState(new MoveToCoreState());
                return;
            }

            enemy.AttackCore();
        }
    }
}
