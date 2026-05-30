using System;
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;

namespace DungeonBuilder.UI.HUD
{
    public sealed class HUDModel : IModel
    {
        private readonly Dictionary<ResourceType, int> _resources = new()
        {
            [ResourceType.Wood] = 0,
            [ResourceType.Stone] = 0,
            [ResourceType.Ore] = 0,
            [ResourceType.Crystal] = 0
        };

        public event Action OnChanged;

        public int Wave { get; private set; }
        public float Countdown { get; private set; }
        public int CoreHealth { get; private set; } = 100;

        public void SetResource(ResourceType type, int value)
        {
            _resources[type] = value;
            OnChanged?.Invoke();
        }

        public int GetResource(ResourceType type)
        {
            return _resources.TryGetValue(type, out int value) ? value : 0;
        }

        public void SetWave(int wave)
        {
            Wave = wave;
            OnChanged?.Invoke();
        }

        public void SetCoreHealth(int coreHealth)
        {
            CoreHealth = coreHealth;
            OnChanged?.Invoke();
        }

        public void SetCountdown(float countdown)
        {
            Countdown = countdown;
            OnChanged?.Invoke();
        }
    }
}
