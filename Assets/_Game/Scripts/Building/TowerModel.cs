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
        private readonly Dictionary<ResourceType, int> _paid = new();

        public event Action OnChanged;

        public int Level { get; private set; } = 1;

        public float Damage     => _data != null ? _data.damage     + _data.damagePerLevel * (Level - 1) : 0f;
        public float Range      => _data != null ? _data.range      + _data.rangePerLevel  * (Level - 1) : 0f;
        public float AttackRate => _data != null ? _data.attackRate : 1f;
        public bool CanUpgrade  => _data != null && Level < _data.maxLevel;

        public IReadOnlyList<ResourceCost> BuildCost   => _data?.buildCost   ?? Array.Empty<ResourceCost>();
        public IReadOnlyList<ResourceCost> UpgradeCost =>
            (IReadOnlyList<ResourceCost>)(_data?.GetUpgradeCostForLevel(Level) ?? Array.Empty<ResourceCost>());

        public int GetPaid(ResourceType type) => _paid.TryGetValue(type, out int v) ? v : 0;

        public bool IsConstructed => BuildCost.Count == 0
            || BuildCost.All(c => GetPaid(c.type) >= c.amount);

        public TowerModel(TowerDataSO data)
        {
            _data = data;
        }

        public void SetLevel(int level)
        {
            Level = level;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Cap nhat so tien da dong gop cho 1 loai resource.
        /// Goi tu BaseTower khi NetworkVariable thay doi.
        /// </summary>
        public void SetPaid(ResourceType type, int amount)
        {
            _paid[type] = amount;
            OnChanged?.Invoke();
        }
    }
}
