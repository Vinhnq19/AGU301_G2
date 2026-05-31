using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    public interface IEnemyState
    {
        void Enter(BaseEnemy enemy);

        void Exit(BaseEnemy enemy);

        void Update(BaseEnemy enemy);
    }
}
