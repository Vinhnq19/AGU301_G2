using System;
using System.Collections.Generic;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;

namespace DungeonBuilder.UI.GameResult
{
    public sealed class GameResultModel : IModel
    {
        public event Action OnChanged;

        public bool IsVisible       { get; private set; }
        public bool IsWin           { get; private set; }
        public int EnemyKillCount   { get; private set; }
        public int BossKillCount    { get; private set; }
        public int WavesCompleted   { get; private set; }
        public int TowersBuilt      { get; private set; }
        public IReadOnlyDictionary<ResourceType, int> FinalResources { get; private set; }

        public void Show(bool isWin, GameStatsTracker stats)
        {
            if (IsVisible) return;
            IsVisible     = true;
            IsWin         = isWin;
            EnemyKillCount  = stats.EnemyKillCount;
            BossKillCount   = stats.BossKillCount;
            WavesCompleted  = stats.WavesCompleted;
            TowersBuilt     = stats.TowersBuilt;
            FinalResources  = stats.FinalResources;
            OnChanged?.Invoke();
        }
    }
}
