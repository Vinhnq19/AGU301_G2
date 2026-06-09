using System;
using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Core
{
    public sealed class EventBus
    {
        public event Action<int> OnCoreHealthChanged;
        public event Action<int> OnWaveStarted;
        public event Action<float> OnPhaseCountdownChanged;
        public event Action<EnemyType> OnEnemyKilled;

        public void RaiseCoreHealthChanged(int currentHealth)
        {
            OnCoreHealthChanged?.Invoke(currentHealth);
        }

        public void RaiseWaveStarted(int wave)
        {
            OnWaveStarted?.Invoke(wave);
        }

        public void RaisePhaseCountdownChanged(float secondsRemaining)
        {
            OnPhaseCountdownChanged?.Invoke(secondsRemaining);
        }

        public void RaiseEnemyKilled(EnemyType enemyType)
        {
            OnEnemyKilled?.Invoke(enemyType);
        }
    }
}
