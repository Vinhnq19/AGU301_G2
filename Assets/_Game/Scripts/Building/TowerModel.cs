using System;
using Assets._Game.Scripts.Data;
using DungeonBuilder.UI.Base;

namespace Assets._Game.Scripts.Building
{
    /// <summary>
    /// Model du lieu cua mot tower cu the (instance). Tinh toan stats theo level tu TowerDataSO.
    /// </summary>
    public sealed class TowerModel : IModel
    {
        private readonly TowerDataSO _data;

        public event Action OnChanged;

        public int Level { get; private set; } = 1;

        public float Damage     => _data != null ? _data.damage + _data.damagePerLevel * (Level - 1) : 0f;
        public float Range      => _data != null ? _data.range  + _data.rangePerLevel  * (Level - 1) : 0f;
        public float AttackRate => _data != null ? _data.attackRate : 1f;
        public bool CanUpgrade  => _data != null && Level < _data.maxLevel;
        public int UpgradeWoodCost => _data != null ? _data.upgradeCostWood : 0;
        public int UpgradeOreCost  => _data != null ? _data.upgradeCostOre  : 0;

        public TowerModel(TowerDataSO data)
        {
            _data = data;
        }

        public void SetLevel(int level)
        {
            Level = level;
            OnChanged?.Invoke();
        }
    }
}
