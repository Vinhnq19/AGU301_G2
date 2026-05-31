using Assets._Game.Scripts.Enemy;

namespace DungeonBuilder.Enemy.States
{
    public sealed class StunnedState : IEnemyState
    {
        private readonly float _duration;
        private float _elapsed;

        public StunnedState(float duration)
        {
            _duration = duration;
        }

        public void Enter(BaseEnemy enemy)
        {
            _elapsed = 0f;
            enemy.PlayStunFeedbackClientRpc();
        }

        public void Exit(BaseEnemy enemy)
        {
        }

        public void Update(BaseEnemy enemy)
        {
            _elapsed += UnityEngine.Time.deltaTime;
            if (_elapsed >= _duration)
            {
                enemy.ChangeState(new MoveToCoreState());
            }
        }
    }
}
