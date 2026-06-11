using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Enums;
using System;
using UnityEngine;

namespace Assets._Game.Scripts.Data
{
    /// <summary>
    /// Chi phi upgrade cho 1 level cu the.
    /// VD: upgradeLevels[0] = cost de len tu lv1 → lv2.
    /// </summary>
    [Serializable]
    public struct UpgradeLevel
    {
        public ResourceCost[] costs;
    }

    /// <summary>
    /// Du lieu cau hinh cua mot loai Tower. Designer chinh trong Inspector.
    /// buildCost[] = cost de dat thap (contribute).
    /// upgradeLevels[] = cost rieng cho moi cap do (index 0 = lv1→lv2, index 1 = lv2→lv3, ...).
    /// </summary>
    [CreateAssetMenu(fileName = "TowerData", menuName = "Dungeon Builder/Data/Tower")]
    public sealed class TowerDataSO : ScriptableObject
    {
        [Header("Base Stats")]
        public TowerType towerType;
        public float maxHealth  = 100f;
        public float damage     = 10f;
        public float range      = 4f;
        public float attackRate = 1f;

        [Header("Bullet")]
        public float bulletSpeed    = 8f;
        public float bulletLifetime = 3f;

        [Header("Build Cost")]
        public ResourceCost[] buildCost = { new ResourceCost(ResourceType.Wood, 25) };

        [Header("Upgrade")]
        public int maxLevel = 3;

        [Tooltip("Index 0 = lv1→lv2, index 1 = lv2→lv3. Size nen bang maxLevel - 1.")]
        public UpgradeLevel[] upgradeLevels;

        // public Sprite icon;

        /// <summary>
        /// Lay danh sach chi phi upgrade tu currentLevel len currentLevel+1.
        /// Tra ve mang rong neu da max level hoac chua cau hinh.
        /// </summary>
        public ResourceCost[] GetUpgradeCostForLevel(int currentLevel)
        {
            int index = currentLevel - 1;
            if (upgradeLevels == null || index < 0 || index >= upgradeLevels.Length)
                return Array.Empty<ResourceCost>();
            return upgradeLevels[index].costs ?? Array.Empty<ResourceCost>();
        }

        /// <summary>Lay build cost cua 1 loai resource cu the.</summary>
        public int GetBuildAmount(ResourceType type)
        {
            foreach (ResourceCost c in buildCost)
                if (c.type == type) return c.amount;
            return 0;
        }
    }
}
