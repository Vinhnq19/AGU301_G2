using Assets._Game.Scripts.Enemy;
using DungeonBuilder.Enemy.States;

namespace DungeonBuilder.Enemy
{
    public sealed class EnemyStateMachine
    {
        private readonly BaseEnemy _enemy;
        private IEnemyState _currentState;

        public EnemyStateMachine(BaseEnemy enemy)
        {
            _enemy = enemy;
        }

        public void ChangeState(IEnemyState nextState)
        {
            if (_currentState == nextState)
            {
                return;
            }

            _currentState?.Exit(_enemy);
            _currentState = nextState;
            _currentState?.Enter(_enemy);
        }

        public void Update()
        {
            _currentState?.Update(_enemy);
        }
    }
}
