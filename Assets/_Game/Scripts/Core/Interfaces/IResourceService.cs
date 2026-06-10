using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Core.Interfaces
{
    public interface IResourceService
    {
        event Action<ResourceChanged> ResourceChanged;

        int GetAmount(ResourceType type);
        IReadOnlyDictionary<ResourceType, int> GetSnapshot();
        bool CanAfford(IReadOnlyList<ResourceCost> costs);

        bool TrySet(ResourceType type, int amount);
        bool TryAdd(ResourceType type, int amount);
        bool TrySpend(IReadOnlyList<ResourceCost> costs);
        bool TryReset(ResourceType type);
        bool TryResetAll();
    }
}
