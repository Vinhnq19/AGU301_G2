using System.Collections.Generic;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Networking
{
    public sealed class SharedResourceManager : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _wood = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _stone = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _ore = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _crystal = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Dictionary<ResourceType, NetworkVariable<int>> _resources;
        private EventBus _eventBus;

        private void Awake()
        {
            BuildResourceMap();
        }

        [Inject]
        public void Construct(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public override void OnNetworkSpawn()
        {
            _wood.OnValueChanged += HandleWoodChanged;
            _stone.OnValueChanged += HandleStoneChanged;
            _ore.OnValueChanged += HandleOreChanged;
            _crystal.OnValueChanged += HandleCrystalChanged;

            RaiseAllCurrentValues();
        }

        public override void OnNetworkDespawn()
        {
            _wood.OnValueChanged -= HandleWoodChanged;
            _stone.OnValueChanged -= HandleStoneChanged;
            _ore.OnValueChanged -= HandleOreChanged;
            _crystal.OnValueChanged -= HandleCrystalChanged;
        }

        public void AddResource(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0)
            {
                return;
            }

            NetworkVariable<int> variable = GetVariable(type);
            variable.Value += amount;
            DBLog.Info($"resource.add.{type}", $"Resource added. type={type}, amount={amount}, total={variable.Value}.", 0.2f, this);
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (!IsServer || amount < 0)
            {
                return false;
            }

            NetworkVariable<int> variable = GetVariable(type);
            if (variable.Value < amount)
            {
                return false;
            }

            variable.Value -= amount;
            DBLog.Info($"resource.spend.{type}", $"Resource spent. type={type}, amount={amount}, total={variable.Value}.", 0.2f, this);
            return true;
        }

        public bool CanAfford(ResourceType type, int amount)
        {
            return GetAmount(type) >= amount;
        }

        public int GetAmount(ResourceType type)
        {
            return GetVariable(type).Value;
        }

        private NetworkVariable<int> GetVariable(ResourceType type)
        {
            if (_resources == null)
            {
                BuildResourceMap();
            }

            if (_resources.TryGetValue(type, out NetworkVariable<int> variable))
            {
                return variable;
            }

            Debug.LogError($"[{nameof(SharedResourceManager)}] No NetworkVariable for ResourceType.{type}. Check BuildResourceMap().", this);
            return _wood;
        }

        private void BuildResourceMap()
        {
            _resources = new Dictionary<ResourceType, NetworkVariable<int>>
            {
                [ResourceType.Wood] = _wood,
                [ResourceType.Stone] = _stone,
                [ResourceType.Ore] = _ore,
                [ResourceType.Crystal] = _crystal
            };
        }

        private void RaiseAllCurrentValues()
        {
            _eventBus?.RaiseResourceUpdated(ResourceType.Wood, _wood.Value);
            _eventBus?.RaiseResourceUpdated(ResourceType.Stone, _stone.Value);
            _eventBus?.RaiseResourceUpdated(ResourceType.Ore, _ore.Value);
            _eventBus?.RaiseResourceUpdated(ResourceType.Crystal, _crystal.Value);
        }

        private void HandleWoodChanged(int previousValue, int newValue)
        {
            _eventBus?.RaiseResourceUpdated(ResourceType.Wood, newValue);
            DBLog.Info("resource.update.Wood", $"Resource updated on client. type=Wood, value={newValue}.", 0.5f, this);
        }

        private void HandleStoneChanged(int previousValue, int newValue)
        {
            _eventBus?.RaiseResourceUpdated(ResourceType.Stone, newValue);
            DBLog.Info("resource.update.Stone", $"Resource updated on client. type=Stone, value={newValue}.", 0.5f, this);
        }

        private void HandleOreChanged(int previousValue, int newValue)
        {
            _eventBus?.RaiseResourceUpdated(ResourceType.Ore, newValue);
            DBLog.Info("resource.update.Ore", $"Resource updated on client. type=Ore, value={newValue}.", 0.5f, this);
        }

        private void HandleCrystalChanged(int previousValue, int newValue)
        {
            _eventBus?.RaiseResourceUpdated(ResourceType.Crystal, newValue);
            DBLog.Info("resource.update.Crystal", $"Resource updated on client. type=Crystal, value={newValue}.", 0.5f, this);
        }
    }
}
