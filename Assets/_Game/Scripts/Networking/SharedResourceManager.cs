using System.Collections.Generic;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Networking
{
    /// <summary>
    /// Quan ly tai nguyen chung toan doi. Moi loai tai nguyen la 1 NetworkVariable
    /// de dam bao dong bo real-time tren tat ca client.
    /// Server la nguon su that duy nhat (WritePermission.Server).
    /// </summary>
    public sealed class SharedResourceManager : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _wood       = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _stone      = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _ore        = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _crystal    = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _copper     = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _iron       = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _blueGems   = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _purpleGems = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Dictionary<ResourceType, NetworkVariable<int>> _resources;

        // Luu tham chieu delegate de co the unsubscribe chinh xac
        private readonly Dictionary<ResourceType, NetworkVariable<int>.OnValueChangedDelegate> _handlers = new();

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
            foreach (var pair in _resources)
            {
                ResourceType type = pair.Key;
                NetworkVariable<int> variable = pair.Value;

                NetworkVariable<int>.OnValueChangedDelegate handler = (_, newValue) =>
                {
                    _eventBus?.RaiseResourceUpdated(type, newValue);
                    DBLog.Info($"resource.update.{type}", $"[SharedResourceManager] Updated. type={type}, value={newValue}.", 0.5f, this);
                };

                _handlers[type] = handler;
                variable.OnValueChanged += handler;
            }

            RaiseAllCurrentValues();
        }

        public override void OnNetworkDespawn()
        {
            foreach (var pair in _resources)
            {
                if (_handlers.TryGetValue(pair.Key, out var handler))
                {
                    pair.Value.OnValueChanged -= handler;
                }
            }

            _handlers.Clear();
        }

        public void AddResource(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0) return;

            NetworkVariable<int> variable = GetVariable(type);
            variable.Value += amount;
            DBLog.Info($"resource.add.{type}", $"[SharedResourceManager] Added. type={type}, amount={amount}, total={variable.Value}.", 0.2f, this);
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (!IsServer || amount < 0) return false;

            NetworkVariable<int> variable = GetVariable(type);
            if (variable.Value < amount) return false;

            variable.Value -= amount;
            DBLog.Info($"resource.spend.{type}", $"[SharedResourceManager] Spent. type={type}, amount={amount}, total={variable.Value}.", 0.2f, this);
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
            if (_resources == null) BuildResourceMap();

            if (_resources.TryGetValue(type, out NetworkVariable<int> variable))
                return variable;

            Debug.LogError($"[SharedResourceManager] No NetworkVariable for ResourceType.{type}. Check BuildResourceMap().", this);
            return _wood;
        }

        private void BuildResourceMap()
        {
            _resources = new Dictionary<ResourceType, NetworkVariable<int>>
            {
                [ResourceType.Wood]       = _wood,
                [ResourceType.Stone]      = _stone,
                [ResourceType.Ore]        = _ore,
                [ResourceType.Crystal]    = _crystal,
                [ResourceType.Copper]     = _copper,
                [ResourceType.Iron]       = _iron,
                [ResourceType.BlueGems]   = _blueGems,
                [ResourceType.PurpleGems] = _purpleGems,
            };
        }

        private void RaiseAllCurrentValues()
        {
            foreach (var pair in _resources)
            {
                _eventBus?.RaiseResourceUpdated(pair.Key, pair.Value.Value);
            }
        }
    }
}
