using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Dungeon Builder/Data/Enemy")]
    public sealed class EnemyDataSO : ScriptableObject
    {
        public EnemyType enemyType;
        public float maxHealth = 100f;
        public float moveSpeed = 2f;
        public int rewardGold = 10;
    }
}
