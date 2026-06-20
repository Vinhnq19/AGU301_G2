#if UNITY_EDITOR
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using UnityEditor;
using UnityEngine;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Gán minWaveToAppear cho mọi ResourceNodeDataSO trong project theo bảng mặc định,
    /// để node loại hiếm bị khóa (ẩn) cho tới wave mở khóa. Loại có giá trị 1 nghĩa là
    /// luôn xuất hiện ngay từ đầu.
    /// </summary>
    public static class ResourceNodeWaveGateTool
    {
        // Bảng wave mở khóa mặc định theo loại. Chỉnh ở đây rồi chạy lại nếu muốn đổi.
        private static readonly Dictionary<ResourceType, int> MinWaveByType = new()
        {
            { ResourceType.Wood, 1 },
            { ResourceType.Stone, 1 },
            { ResourceType.Ore, 2 },
            { ResourceType.Copper, 2 },
            { ResourceType.Iron, 3 },
            { ResourceType.Crystal, 3 },
            { ResourceType.BlueGems, 5 },
            { ResourceType.PurpleGems, 6 },
        };

        [MenuItem("Dungeon Builder/Apply Resource Wave Gate")]
        public static void Apply()
        {
            string[] guids = AssetDatabase.FindAssets("t:ResourceNodeDataSO");
            int updated = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ResourceNodeDataSO data = AssetDatabase.LoadAssetAtPath<ResourceNodeDataSO>(path);
                if (data == null)
                {
                    continue;
                }

                if (MinWaveByType.TryGetValue(data.resourceType, out int minWave))
                {
                    data.minWaveToAppear = Mathf.Max(1, minWave);
                }
                else
                {
                    data.minWaveToAppear = Mathf.Max(1, data.minWaveToAppear);
                }

                EditorUtility.SetDirty(data);
                updated++;
                Debug.Log($"[ResourceNodeWaveGateTool] {path}: {data.resourceType} -> minWaveToAppear={data.minWaveToAppear}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ResourceNodeWaveGateTool] Đã cập nhật {updated} ResourceNodeDataSO.");
        }
    }
}
#endif
