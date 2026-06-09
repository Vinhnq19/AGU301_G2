using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Core
{
    public sealed class ResourceStore
    {
        private readonly Dictionary<ResourceType, int> _amounts = new();

        public ResourceStore()
        {
            foreach (ResourceType type in ResourceTypeUtility.All)
            {
                _amounts[type] = 0;
            }
        }

        public int GetAmount(ResourceType type)
        {
            return _amounts.TryGetValue(type, out int amount) ? amount : 0;
        }

        public IReadOnlyDictionary<ResourceType, int> GetSnapshot()
        {
            return new Dictionary<ResourceType, int>(_amounts);
        }

        public bool CanAfford(IReadOnlyList<ResourceCost> costs)
        {
            if (!TryAggregateCosts(costs, out Dictionary<ResourceType, int> totals))
            {
                return false;
            }

            foreach (var pair in totals)
            {
                if (GetAmount(pair.Key) < pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TrySet(ResourceType type, int amount)
        {
            if (!ResourceTypeUtility.IsValid(type) || amount < 0)
            {
                return false;
            }

            _amounts[type] = amount;
            return true;
        }

        public bool TryAdd(ResourceType type, int amount)
        {
            if (!ResourceTypeUtility.IsValid(type) || amount < 0)
            {
                return false;
            }

            int current = GetAmount(type);
            if (amount > int.MaxValue - current)
            {
                return false;
            }

            _amounts[type] = current + amount;
            return true;
        }

        public bool TrySpend(IReadOnlyList<ResourceCost> costs)
        {
            if (!TryAggregateCosts(costs, out Dictionary<ResourceType, int> totals))
            {
                return false;
            }

            foreach (var pair in totals)
            {
                if (GetAmount(pair.Key) < pair.Value)
                {
                    return false;
                }
            }

            foreach (var pair in totals)
            {
                _amounts[pair.Key] -= pair.Value;
            }

            return true;
        }

        public bool TryReset(ResourceType type)
        {
            return TrySet(type, 0);
        }

        public void ResetAll()
        {
            foreach (ResourceType type in ResourceTypeUtility.All)
            {
                _amounts[type] = 0;
            }
        }

        private static bool TryAggregateCosts(
            IReadOnlyList<ResourceCost> costs,
            out Dictionary<ResourceType, int> totals)
        {
            totals = new Dictionary<ResourceType, int>();
            if (costs == null)
            {
                return false;
            }

            foreach (ResourceCost cost in costs)
            {
                if (!ResourceTypeUtility.IsValid(cost.type) || cost.amount < 0)
                {
                    return false;
                }

                totals.TryGetValue(cost.type, out int current);
                if (cost.amount > int.MaxValue - current)
                {
                    return false;
                }

                totals[cost.type] = current + cost.amount;
            }

            return true;
        }
    }
}
