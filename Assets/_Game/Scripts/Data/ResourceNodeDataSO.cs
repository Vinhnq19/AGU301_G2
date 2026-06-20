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

        [Tooltip("Wave nhỏ nhất loại này bắt đầu xuất hiện/khai thác được. Ví dụ BlueGems = 5 → node bị khóa (ẩn) cho tới hết wave 4.")]
        [Min(1)] public int minWaveToAppear = 1;
    }
}
