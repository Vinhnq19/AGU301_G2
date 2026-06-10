using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;

namespace DungeonBuilder.Networking
{
    /// <summary>
    /// Server-authoritative shared resource inventory for the current match.
    /// ResourceType is the source of truth for the available resource kinds.
    /// </summary>
    public sealed class SharedResourceManager : NetworkBehaviour, IResourceService
    {
        private readonly NetworkList<ResourceAmount> _resources = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly ResourceStore _store = new();

        public event Action<ResourceChanged> ResourceChanged;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                InitializeNetworkList();
            }

            SyncStoreFromNetworkList();
            _resources.OnListChanged += HandleListChanged;
            RaiseAllCurrentValues();
        }

        public override void OnNetworkDespawn()
        {
            _resources.OnListChanged -= HandleListChanged;
        }

        public int GetAmount(ResourceType type)
        {
            return _store.GetAmount(type);
        }

        public IReadOnlyDictionary<ResourceType, int> GetSnapshot()
        {
            return _store.GetSnapshot();
        }

        public bool CanAfford(IReadOnlyList<ResourceCost> costs)
        {
            return _store.CanAfford(costs);
        }

        public bool TrySet(ResourceType type, int amount)
        {
            if (!IsServer || !_store.TrySet(type, amount))
            {
                return false;
            }

            SyncNetworkListFromStore();
            DBLog.Info($"resource.set.{type}", $"[SharedResourceManager] Set. type={type}, total={amount}.", 0.2f, this);
            return true;
        }

        public bool TryAdd(ResourceType type, int amount)
        {
            if (!IsServer || !_store.TryAdd(type, amount))
            {
                return false;
            }

            SyncNetworkListFromStore();
            DBLog.Info($"resource.add.{type}", $"[SharedResourceManager] Added. type={type}, amount={amount}, total={GetAmount(type)}.", 0.2f, this);
            return true;
        }

        public bool TrySpend(IReadOnlyList<ResourceCost> costs)
        {
            if (!IsServer || !_store.TrySpend(costs))
            {
                return false;
            }

            SyncNetworkListFromStore();
            DBLog.Info("resource.spend", "[SharedResourceManager] Atomic resource spend completed.", 0.2f, this);
            return true;
        }

        public bool TryReset(ResourceType type)
        {
            if (!IsServer || !_store.TryReset(type))
            {
                return false;
            }

            SyncNetworkListFromStore();
            return true;
        }

        public bool TryResetAll()
        {
            if (!IsServer)
            {
                return false;
            }

            _store.ResetAll();
            SyncNetworkListFromStore();
            return true;
        }

        private void InitializeNetworkList()
        {
            var existing = new HashSet<ResourceType>();
            for (int i = _resources.Count - 1; i >= 0; i--)
            {
                ResourceType type = _resources[i].Type;
                if (!ResourceTypeUtility.IsValid(type) || !existing.Add(type))
                {
                    _resources.RemoveAt(i);
                }
            }

            foreach (ResourceType type in ResourceTypeUtility.All)
            {
                if (!existing.Contains(type))
                {
                    _resources.Add(new ResourceAmount(type, 0));
                }
            }
        }

        private void SyncStoreFromNetworkList()
        {
            _store.ResetAll();
            foreach (ResourceAmount resource in _resources)
            {
                _store.TrySet(resource.Type, resource.Amount);
            }
        }

        private void SyncNetworkListFromStore()
        {
            for (int i = 0; i < _resources.Count; i++)
            {
                ResourceAmount resource = _resources[i];
                int amount = _store.GetAmount(resource.Type);
                if (resource.Amount != amount)
                {
                    _resources[i] = new ResourceAmount(resource.Type, amount);
                }
            }
        }

        private void HandleListChanged(NetworkListEvent<ResourceAmount> changeEvent)
        {
            if (changeEvent.Type != NetworkListEvent<ResourceAmount>.EventType.Value)
            {
                SyncStoreFromNetworkList();
                RaiseAllCurrentValues();
                return;
            }

            _store.TrySet(changeEvent.Value.Type, changeEvent.Value.Amount);
            RaiseChanged(
                changeEvent.Value.Type,
                changeEvent.PreviousValue.Amount,
                changeEvent.Value.Amount);
        }

        private void RaiseAllCurrentValues()
        {
            foreach (var pair in _store.GetSnapshot())
            {
                RaiseChanged(pair.Key, pair.Value, pair.Value);
            }
        }

        private void RaiseChanged(ResourceType type, int previousAmount, int currentAmount)
        {
            ResourceChanged?.Invoke(new ResourceChanged(type, previousAmount, currentAmount));
            DBLog.Info(
                $"resource.update.{type}",
                $"[SharedResourceManager] Updated. type={type}, value={currentAmount}.",
                0.5f,
                this);
        }
    }
}
