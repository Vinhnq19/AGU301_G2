namespace DungeonBuilder.Enemy.States
{
    public sealed class AttackWallState : IEnemyState
    {
        public void Enter(BaseEnemy enemy)
        {
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            if (!enemy.IsBlockedByWall())
            {
                enemy.ChangeState(new MoveToCoreState());
                return;
            }

            enemy.AttackCurrentBlocker();
        }
    }
}
