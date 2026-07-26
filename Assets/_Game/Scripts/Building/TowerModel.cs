using System;
using System.Collections.Generic;
using System.Linq;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Enums;

namespace Assets._Game.Scripts.Building
{
    /// <summary>
    /// Model du lieu cua mot tower instance. Tinh toan stats theo level tu TowerDataSO.
    /// Theo doi tien do xay dung qua dictionary _paid (cap nhat tu BaseTower NetworkVariable).
    /// </summary>
    public sealed class TowerModel
    {
        private readonly TowerDataSO _data;
        public TowerDataSO Data => _data;

        public event Action OnChanged;

        public int Level { get; private set; } = 1;
        public float CurrentHealth { get; private set; }

        private float GetHealthMultiplier() => Level == 1 ? 1f : (Level == 2 ? 1.3f : 1.6f);
        private float GetDamageMultiplier() => Level == 1 ? 1f : (Level == 2 ? 1.5f : 2f);
        private float GetRangeMultiplier() => Level == 1 ? 1f : (Level == 2 ? 1.08f : 1.15f);
        private float GetAttackRateMultiplier() => Level == 1 ? 1f : (Level == 2 ? 1.15f : 1.3f);

        public float MaxHealth  => _data != null ? _data.maxHealth * GetHealthMultiplier() : 100f;
        public float Damage     => _data != null ? _data.damage * GetDamageMultiplier() : 0f;
        public float Range      => _data != null ? _data.range * GetRangeMultiplier() : 0f;
        public float AttackRate => _data != null ? _data.attackRate * GetAttackRateMultiplier() : 1f;
        public bool CanUpgrade  => _data != null && Level < _data.maxLevel;

        public IReadOnlyList<ResourceCost> BuildCost   => _data?.buildCost   ?? Array.Empty<ResourceCost>();
        public IReadOnlyList<ResourceCost> UpgradeCost =>
            (IReadOnlyList<ResourceCost>)(_data?.GetUpgradeCostForLevel(Level) ?? Array.Empty<ResourceCost>());

        public TowerModel(TowerDataSO data)
        {
            _data = data;
        }

        public void SetLevel(int level)
        {
            Level = level;
            OnChanged?.Invoke();
        }

        public void SetHealth(float health)
        {
            CurrentHealth = health;
            OnChanged?.Invoke();
        }
    }
}
