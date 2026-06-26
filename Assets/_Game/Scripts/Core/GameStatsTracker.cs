using System;
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;

namespace DungeonBuilder.Core
{
    public sealed class GameStatsTracker : IDisposable
    {
        public int EnemyKillCount { get; private set; }
        public int BossKillCount  { get; private set; }
        public int WavesCompleted { get; private set; }
        public int TowersBuilt   { get; private set; }
        public IReadOnlyDictionary<ResourceType, int> FinalResources { get; private set; }

        private readonly EventBus _eventBus;
        private readonly IResourceService _resourceService;

        public GameStatsTracker(EventBus eventBus, IResourceService resourceService)
        {
            _eventBus = eventBus;
            _resourceService = resourceService;

            _eventBus.OnEnemyKilled  += HandleEnemyKilled;
            _eventBus.OnWaveStarted  += HandleWaveStarted;
            _eventBus.OnTowerPlaced  += HandleTowerPlaced;
            _eventBus.OnGameEnded    += HandleGameEnded;
        }

        public void Dispose()
        {
            _eventBus.OnEnemyKilled  -= HandleEnemyKilled;
            _eventBus.OnWaveStarted  -= HandleWaveStarted;
            _eventBus.OnTowerPlaced  -= HandleTowerPlaced;
            _eventBus.OnGameEnded    -= HandleGameEnded;
        }

        private void HandleEnemyKilled(EnemyType type, bool isBoss)
        {
            EnemyKillCount++;
            if (isBoss) BossKillCount++;
        }

        private void HandleWaveStarted(int wave, bool isBossWave)
        {
            if (wave > WavesCompleted) WavesCompleted = wave;
        }

        private void HandleTowerPlaced()
        {
            TowersBuilt++;
        }

        private void HandleGameEnded(bool isWin)
        {
            FinalResources = _resourceService.GetSnapshot();
        }
    }
}
