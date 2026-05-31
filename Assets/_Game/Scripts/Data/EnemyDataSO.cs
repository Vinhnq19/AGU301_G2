using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace Assets._Game.Scripts.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Dungeon Builder/Data/Enemy")]
    public sealed class EnemyDataSO : ScriptableObject
    {
        public EnemyType enemyType;
        public float maxHealth = 100f;
        public float moveSpeed = 2f;
        public int rewardToken = 10;
    }
}
