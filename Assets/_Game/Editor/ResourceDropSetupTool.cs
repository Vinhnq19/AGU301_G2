#if UNITY_EDITOR
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Harvesting;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Sinh prefab ResourceDrop RIÊNG cho từng loại tài nguyên (visual/màu khác nhau),
    /// thay cho việc dùng chung một DB_ResourceDrop. Tool:
    /// - Duyệt mọi node prefab trong Harvesting/, đọc resourceType từ ResourceNodeDataSO của node.
    /// - Với mỗi loại: clone DB_ResourceDrop -> DB_{Type}Drop, tô màu visual theo loại.
    /// - Gán drop prefab tương ứng vào _resourceDropPrefab của node.
    /// - Đăng ký drop prefab vào NetworkObjectPool + NetworkPrefabs list.
    ///
    /// Idempotent: chạy lại không tạo trùng. Logic cộng tài nguyên vẫn do node gọi
    /// drop.Configure(type, amount) lúc spawn — prefab riêng chỉ khác ở visual.
    /// </summary>
    public static class ResourceDropSetupTool
    {
        private const string HarvestingRoot = "Assets/_Game/Generated/Prefabs/Harvesting";
        private const string TemplateDropPath = "Assets/_Game/Generated/Prefabs/Harvesting/DB_ResourceDrop.prefab";
        private const string NetworkPrefabsPath = "Assets/_Game/Generated/DB_NetworkPrefabs.asset";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Dungeon Builder/Setup Per-Type Resource Drops")]
        public static void Setup()
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateDropPath);
            if (template == null)
            {
                Debug.LogError($"[ResourceDropSetupTool] Không tìm thấy prefab template {TemplateDropPath}. Chạy Bootstrap trước.");
                return;
            }

            // Map loại -> prefab node, đọc từ data của node (không hardcode).
            Dictionary<ResourceType, string> nodePrefabByType = MapNodePrefabsByType();
            if (nodePrefabByType.Count == 0)
            {
                Debug.LogWarning("[ResourceDropSetupTool] Không tìm thấy node prefab nào có ResourceNodeDataSO hợp lệ.");
                return;
            }

            var dropPrefabs = new List<NetworkObject>();

            foreach (KeyValuePair<ResourceType, string> pair in nodePrefabByType)
            {
                ResourceType type = pair.Key;
                string nodePrefabPath = pair.Value;

                NetworkObject drop = EnsureDropPrefab(type, template);
                if (drop == null)
                {
                    continue;
                }

                dropPrefabs.Add(drop);
                AssignDropToNode(nodePrefabPath, drop);
            }

            RegisterInPoolAndNetwork(dropPrefabs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ResourceDropSetupTool] Hoàn tất. Đã tạo/gán {dropPrefabs.Count} drop prefab riêng theo loại.");
        }

        private static Dictionary<ResourceType, string> MapNodePrefabsByType()
        {
            var result = new Dictionary<ResourceType, string>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { HarvestingRoot });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    continue;
                }

                HarvestableNode node = go.GetComponent<HarvestableNode>();
                if (node == null)
                {
                    continue; // bỏ qua DB_ResourceDrop và các drop prefab.
                }

                var serialized = new SerializedObject(node);
                ResourceNodeDataSO data = serialized.FindProperty("_data")?.objectReferenceValue as ResourceNodeDataSO;
                if (data == null)
                {
                    Debug.LogWarning($"[ResourceDropSetupTool] Node {path} thiếu _data, bỏ qua.");
                    continue;
                }

                // Một loại có thể có nhiều node prefab; chỉ giữ map đầu tiên cho việc tạo drop,
                // nhưng vẫn gán drop cho TẤT CẢ node của loại đó ở bước sau.
                if (!result.ContainsKey(data.resourceType))
                {
                    result[data.resourceType] = path;
                }
            }

            return result;
        }

        private static NetworkObject EnsureDropPrefab(ResourceType type, GameObject template)
        {
            string dropPath = $"{HarvestingRoot}/DB_{type}Drop.prefab";

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(dropPath);
            if (existing == null)
            {
                if (!AssetDatabase.CopyAsset(TemplateDropPath, dropPath))
                {
                    Debug.LogWarning($"[ResourceDropSetupTool] Không clone được drop prefab cho {type}.");
                    return null;
                }

                AssetDatabase.ImportAsset(dropPath, ImportAssetOptions.ForceUpdate);
            }

            // Tô màu visual theo loại.
            GameObject instance = PrefabUtility.LoadPrefabContents(dropPath);
            try
            {
                SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.color = ColorForType(type);
                }

                PrefabUtility.SaveAsPrefabAsset(instance, dropPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(dropPath);
            return saved != null ? saved.GetComponent<NetworkObject>() : null;
        }

        /// <summary>Gán drop prefab cho TẤT CẢ node prefab thuộc loại tương ứng.</summary>
        private static void AssignDropToNode(string nodePrefabPath, NetworkObject drop)
        {
            // Gán cho mọi node cùng loại, không chỉ node đại diện.
            ResourceType targetType = ReadNodeType(nodePrefabPath);
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { HarvestingRoot });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<HarvestableNode>() == null)
                {
                    continue;
                }

                if (ReadNodeType(path) != targetType)
                {
                    continue;
                }

                GameObject instance = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    HarvestableNode node = instance.GetComponent<HarvestableNode>();
                    var serialized = new SerializedObject(node);
                    SerializedProperty prop = serialized.FindProperty("_resourceDropPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = drop;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
        }

        private static ResourceType ReadNodeType(string nodePrefabPath)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(nodePrefabPath);
            HarvestableNode node = go != null ? go.GetComponent<HarvestableNode>() : null;
            if (node == null)
            {
                return ResourceType.MAX;
            }

            var serialized = new SerializedObject(node);
            ResourceNodeDataSO data = serialized.FindProperty("_data")?.objectReferenceValue as ResourceNodeDataSO;
            return data != null ? data.resourceType : ResourceType.MAX;
        }

        private static Color ColorForType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return new Color(0.45f, 0.30f, 0.15f);
                case ResourceType.Stone: return new Color(0.60f, 0.60f, 0.65f);
                case ResourceType.Ore: return new Color(0.40f, 0.35f, 0.30f);
                case ResourceType.Crystal: return new Color(0.70f, 0.30f, 1.00f);
                case ResourceType.Copper: return new Color(0.85f, 0.50f, 0.25f);
                case ResourceType.Iron: return new Color(0.75f, 0.78f, 0.82f);
                case ResourceType.BlueGems: return new Color(0.30f, 0.55f, 1.00f);
                case ResourceType.PurpleGems: return new Color(0.65f, 0.30f, 0.90f);
                case ResourceType.Token: return new Color(1.00f, 0.85f, 0.25f);
                case ResourceType.Coin: return new Color(1.00f, 0.78f, 0.10f);
                default: return Color.white;
            }
        }

        private static void RegisterInPoolAndNetwork(List<NetworkObject> dropPrefabs)
        {
            Scene scene = GetOrOpenScene();

            NetworkObjectPool pool = Object.FindFirstObjectByType<NetworkObjectPool>();
            if (pool != null)
            {
                Transform poolRoot = pool.transform.Find("PoolRoot");
                if (poolRoot == null) poolRoot = pool.transform;
                AppendPoolEntries(pool, dropPrefabs, poolRoot, prewarmCount: 8);
            }
            else
            {
                Debug.LogWarning("[ResourceDropSetupTool] Không tìm thấy NetworkObjectPool trong scene.");
            }

            RegisterNetworkPrefabs(dropPrefabs);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AppendPoolEntries(NetworkObjectPool pool, List<NetworkObject> prefabs, Transform parent, int prewarmCount)
        {
            var serialized = new SerializedObject(pool);
            SerializedProperty entries = serialized.FindProperty("_entries");

            var existing = new HashSet<Object>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                Object p = entries.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue;
                if (p != null) existing.Add(p);
            }

            foreach (NetworkObject prefab in prefabs)
            {
                if (prefab == null || existing.Contains(prefab.gameObject))
                {
                    continue;
                }

                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("_prewarmCount").intValue = prewarmCount;
                entry.FindPropertyRelative("_parent").objectReferenceValue = parent;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pool);
        }

        private static void RegisterNetworkPrefabs(List<NetworkObject> prefabs)
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                Debug.LogWarning($"[ResourceDropSetupTool] Không tìm thấy NetworkPrefabsList tại {NetworkPrefabsPath}.");
                return;
            }

            var existing = new HashSet<GameObject>();
            foreach (NetworkPrefab np in list.PrefabList)
            {
                if (np.Prefab != null) existing.Add(np.Prefab);
            }

            foreach (NetworkObject prefab in prefabs)
            {
                if (prefab == null || existing.Contains(prefab.gameObject))
                {
                    continue;
                }

                list.Add(new NetworkPrefab
                {
                    Override = NetworkPrefabOverride.None,
                    Prefab = prefab.gameObject
                });
            }

            EditorUtility.SetDirty(list);
        }

        private static Scene GetOrOpenScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == ScenePath && active.isLoaded)
            {
                return active;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
#endif
