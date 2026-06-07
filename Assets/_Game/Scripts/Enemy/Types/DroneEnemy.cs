using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.Types
{
    public sealed class DroneEnemy : BaseEnemy
    {
        public override bool IsBlockedByWall()
        {
            // Drone flies over walls/ignores wall blockages completely
            return false;
        }
    }
}
