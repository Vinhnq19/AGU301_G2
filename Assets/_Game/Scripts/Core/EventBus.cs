using System;
using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Core
{
    public sealed class EventBus
    {
        public event Action<int> OnCoreHealthChanged;
        public event Action<int, bool> OnWaveStarted;
        public event Action<float> OnPhaseCountdownChanged;
        public event Action<GamePhase> OnGamePhaseChanged;
        public event Action<EnemyType, bool> OnEnemyKilled;

        public void RaiseCoreHealthChanged(int currentHealth)
        {
            OnCoreHealthChanged?.Invoke(currentHealth);
        }

        public void RaiseWaveStarted(int wave, bool isBossWave)
        {
            OnWaveStarted?.Invoke(wave, isBossWave);
        }

        public void RaisePhaseCountdownChanged(float secondsRemaining)
        {
            OnPhaseCountdownChanged?.Invoke(secondsRemaining);
        }

        public void RaiseGamePhaseChanged(GamePhase phase)
        {
            OnGamePhaseChanged?.Invoke(phase);
        }

        public void RaiseEnemyKilled(EnemyType enemyType, bool isBoss)
        {
            OnEnemyKilled?.Invoke(enemyType, isBoss);
        }

        public event Action<bool> OnGameEnded;

        public void RaiseGameEnded(bool isWin)
        {
            OnGameEnded?.Invoke(isWin);
        }
    }
}
