using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Data
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "Dungeon Builder/Data/Tower")]
    public sealed class TowerDataSO : ScriptableObject
    {
        public TowerType towerType;
        public float damage = 10f;
        public float range = 4f;
        public float attackRate = 1f;
        public int woodCost = 25;
        public int oreCost = 0;
    }
}
