using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Data
{
    [CreateAssetMenu(fileName = "ResourceNodeData", menuName = "Dungeon Builder/Data/Resource Node")]
    public sealed class ResourceNodeDataSO : ScriptableObject
    {
        public ResourceType resourceType;
        public int hitsToBreak = 5;
        public int amountPerHit = 20;
        public int maxAmount = 100;
        public float respawnTime = 10f;
    }
}
