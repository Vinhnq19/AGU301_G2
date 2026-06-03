using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Data
{
    [System.Serializable]
    public struct SpawnGroup
    {
        public EnemyType enemyType;
        public int count;
        public float spawnInterval;
        public int spawnPointIndex;
        public int pathIndex;
    }

    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Dungeon Builder/Data/Wave Config")]
    public sealed class WaveSO : ScriptableObject
    {
        public float buildPhaseDuration = 30f;
        public List<SpawnGroup> spawnGroups = new();
    }
}
