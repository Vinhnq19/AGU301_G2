using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace Assets._Game.Scripts.Data
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "Dungeon Builder/Data/Tower")]
    public sealed class TowerDataSO : ScriptableObject
    {
        [Header("Base Stats")]
        public TowerType towerType;
        public float damage = 10f;
        public float range = 4f;
        public float attackRate = 1f;
        public int woodCost = 25;
        public int oreCost = 0;

        [Header("Bullet")]
        public float bulletSpeed = 8f;
        public float bulletLifetime = 3f;

        [Header("Upgrade")]
        public int maxLevel = 3;
        public float damagePerLevel = 5f;
        public float rangePerLevel = 0.5f;
        public int upgradeCostWood = 15;
        public int upgradeCostOre = 5;
        // public Sprite icon;  // TODO: bat comment khi gan icon cho Tower Selection Panel
    }
}

