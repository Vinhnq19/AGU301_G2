using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    /// <summary>
    /// Đi theo đường về core. State KHÔNG giữ dữ liệu riêng nên dùng 1 instance dùng chung
    /// (<see cref="Instance"/>) — tránh cấp phát mỗi lần chuyển state, quan trọng khi có
    /// hàng trăm enemy liên tục đổi qua lại giữa Move và Attack.
    /// </summary>
    public sealed class MoveToCoreState : IEnemyState
    {
        public static readonly MoveToCoreState Instance = new();

        public void Enter(BaseEnemy enemy)
        {
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            enemy.SenseIfDue();

            if (enemy.HasAttackTarget)
            {
                enemy.ChangeState(AttackTargetState.Instance);
                return;
            }

            if (enemy.IsBlockedByWall())
            {
                enemy.ChangeState(AttackWallState.Instance);
                return;
            }

            enemy.MoveTowardsTarget();
        }
    }
}
