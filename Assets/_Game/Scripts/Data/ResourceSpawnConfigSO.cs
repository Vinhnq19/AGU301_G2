using System;
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Data
{
    /// <summary>
    /// Một dòng cấu hình cho một loại tài nguyên: prefab node, dữ liệu node, và
    /// quy luật xuất hiện theo wave (mở khóa từ wave nào, trọng số ban đầu, tốc độ tăng).
    /// </summary>
    [Serializable]
    public struct ResourceSpawnEntry
    {
        public ResourceType resourceType;

        [Tooltip("Prefab HarvestableNode tương ứng. Phải được đăng ký trong NetworkObjectPool và NetworkPrefabs list.")]
        public NetworkObject nodePrefab;

        [Tooltip("Tùy chọn: override ResourceNodeDataSO khi spawn. Để trống thì dùng data gắn sẵn trên prefab.")]
        public ResourceNodeDataSO nodeData;

        [Tooltip("Wave nhỏ nhất loại này bắt đầu có thể xuất hiện. Ví dụ BlueGems = 5 → 4 wave đầu không spawn.")]
        [Min(1)] public int minWaveToAppear;

        [Tooltip("Trọng số (xác suất tương đối) ngay khi vừa mở khóa ở minWaveToAppear.")]
        [Min(0f)] public float baseWeight;

        [Tooltip("Trọng số cộng thêm cho mỗi wave kể từ khi mở khóa. Loại hiếm nên đặt cao hơn để tỉ lệ tăng dần.")]
        [Min(0f)] public float weightGainPerWave;

        [Tooltip("Trần trọng số (0 = không giới hạn).")]
        [Min(0f)] public float maxWeight;
    }

    [CreateAssetMenu(fileName = "ResourceSpawnConfig", menuName = "Dungeon Builder/Data/Resource Spawn Config")]
    public sealed class ResourceSpawnConfigSO : ScriptableObject
    {
        [Header("Resource Entries")]
        public List<ResourceSpawnEntry> entries = new();

        [Header("Số node spawn mỗi wave")]
        [Tooltip("Số node spawn ở wave đầu tiên.")]
        [Min(0)] public int baseNodesPerWave = 2;

        [Tooltip("Số node cộng thêm cho mỗi wave (làm tròn xuống).")]
        [Min(0f)] public float nodesPerWaveGain = 1f;

        [Tooltip("Trần số node spawn mỗi wave.")]
        [Min(0)] public int maxNodesPerWave = 12;

        /// <summary>
        /// Trọng số của một entry tại wave hiện tại. Trả về 0 nếu chưa mở khóa.
        /// </summary>
        public float GetWeight(in ResourceSpawnEntry entry, int wave)
        {
            if (wave < entry.minWaveToAppear)
            {
                return 0f;
            }

            int wavesSinceUnlock = wave - entry.minWaveToAppear;
            float weight = entry.baseWeight + entry.weightGainPerWave * wavesSinceUnlock;
            if (entry.maxWeight > 0f)
            {
                weight = Mathf.Min(weight, entry.maxWeight);
            }

            return Mathf.Max(0f, weight);
        }

        /// <summary>
        /// Số node nên spawn tại wave hiện tại, đã clamp theo maxNodesPerWave.
        /// </summary>
        public int GetNodeCount(int wave)
        {
            int waveIndex = Mathf.Max(0, wave - 1);
            int count = baseNodesPerWave + Mathf.FloorToInt(nodesPerWaveGain * waveIndex);
            return Mathf.Clamp(count, 0, maxNodesPerWave);
        }
    }
}
