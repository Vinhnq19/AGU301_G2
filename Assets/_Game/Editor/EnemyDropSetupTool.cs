#if UNITY_EDITOR
using System.Collections.Generic;
using Assets._Game.Scripts.Enemy;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Tự động thiết lập hệ thống rơi Token cho Kẻ địch (Enemy):
    /// 1. Nhân bản DB_ResourceDrop thành DB_TokenDrop.prefab nếu chưa có.
    /// 2. Tô màu visual của DB_TokenDrop thành màu tím đặc trưng.
    /// 3. Đăng ký DB_TokenDrop vào NetworkObjectPool trong scene và NetworkPrefabsList.
    /// 4. Quét mọi prefab enemy trong Assets/_Game/Generated/Prefabs/Enemies/ và gán tham chiếu DB_TokenDrop vào _tokenDropPrefab.
    /// </summary>
    public static class EnemyDropSetupTool
    {
        private const string HarvestingRoot = "Assets/_Game/Generated/Prefabs/Harvesting";
        private const string TemplateDropPath = "Assets/_Game/Generated/Prefabs/Harvesting/DB_ResourceDrop.prefab";
        private const string TokenDropPath = "Assets/_Game/Generated/Prefabs/Harvesting/DB_TokenDrop.prefab";
        private const string NetworkPrefabsPath = "Assets/_Game/Generated/DB_NetworkPrefabs.asset";
        private const string EnemyPrefabsRoot = "Assets/_Game/Generated/Prefabs/Enemies";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly Color TokenColor = new Color(0.65f, 0.30f, 0.90f); // Màu tím token

        [MenuItem("Dungeon Builder/Setup Enemy Token Drops")]
        public static void Setup()
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateDropPath);
            if (template == null)
            {
                Debug.LogError($"[EnemyDropSetupTool] Không tìm thấy prefab template {TemplateDropPath}. Hãy tạo resource drops trước.");
                return;
            }

            // 1. Đảm bảo có DB_TokenDrop.prefab và tô màu tím
            NetworkObject tokenDrop = EnsureTokenDropPrefab(template);
            if (tokenDrop == null)
            {
                Debug.LogError("[EnemyDropSetupTool] Thất bại khi tạo hoặc load DB_TokenDrop.prefab.");
                return;
            }

            // 2. Đăng ký vào Pool và NetworkPrefabs
            RegisterInPoolAndNetwork(tokenDrop);

            // 3. Gán tham chiếu Token Drop vào các prefab enemy
            AssignTokenDropToEnemies(tokenDrop);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyDropSetupTool] Đã cấu hình xong hệ thống rơi Token tím từ Kẻ địch!");
        }

        private static NetworkObject EnsureTokenDropPrefab(GameObject template)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TokenDropPath);
            if (existing == null)
            {
                if (!AssetDatabase.CopyAsset(TemplateDropPath, TokenDropPath))
                {
                    Debug.LogError($"[EnemyDropSetupTool] Không copy được {TemplateDropPath} sang {TokenDropPath}.");
                    return null;
                }
                AssetDatabase.ImportAsset(TokenDropPath, ImportAssetOptions.ForceUpdate);
            }

            // Tô màu tím cho SpriteRenderer của visual
            GameObject instance = PrefabUtility.LoadPrefabContents(TokenDropPath);
            try
            {
                SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.color = TokenColor;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, TokenDropPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(TokenDropPath);
            return saved != null ? saved.GetComponent<NetworkObject>() : null;
        }

        private static void RegisterInPoolAndNetwork(NetworkObject tokenDrop)
        {
            Scene scene = GetOrOpenScene();

            DungeonBuilder.Networking.Pool.NetworkObjectPool pool = Object.FindFirstObjectByType<DungeonBuilder.Networking.Pool.NetworkObjectPool>();
            if (pool != null)
            {
                Transform poolRoot = pool.transform.Find("PoolRoot");
                if (poolRoot == null) poolRoot = pool.transform;
                AppendPoolEntry(pool, tokenDrop, poolRoot, prewarmCount: 20);
            }
            else
            {
                Debug.LogWarning("[EnemyDropSetupTool] Không tìm thấy NetworkObjectPool trong scene để đăng ký DB_TokenDrop.");
            }

            RegisterNetworkPrefab(tokenDrop);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AppendPoolEntry(DungeonBuilder.Networking.Pool.NetworkObjectPool pool, NetworkObject prefab, Transform parent, int prewarmCount)
        {
            var serialized = new SerializedObject(pool);
            SerializedProperty entries = serialized.FindProperty("_entries");

            bool exists = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                Object p = entries.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue;
                if (p == prefab.gameObject)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("_prewarmCount").intValue = prewarmCount;
                entry.FindPropertyRelative("_parent").objectReferenceValue = parent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pool);
                Debug.Log($"[EnemyDropSetupTool] Đã thêm DB_TokenDrop vào NetworkObjectPool trong scene.");
            }
        }

        private static void RegisterNetworkPrefab(NetworkObject prefab)
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                Debug.LogWarning($"[EnemyDropSetupTool] Không tìm thấy NetworkPrefabsList tại {NetworkPrefabsPath}.");
                return;
            }

            bool exists = false;
            foreach (NetworkPrefab np in list.PrefabList)
            {
                if (np.Prefab == prefab.gameObject)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                list.Add(new NetworkPrefab
                {
                    Override = NetworkPrefabOverride.None,
                    Prefab = prefab.gameObject
                });
                EditorUtility.SetDirty(list);
                Debug.Log("[EnemyDropSetupTool] Đã đăng ký DB_TokenDrop vào NetworkPrefabsList.");
            }
        }

        private static void AssignTokenDropToEnemies(NetworkObject tokenDrop)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabsRoot });
            int assignedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                BaseEnemy enemy = go.GetComponent<BaseEnemy>();
                if (enemy == null) continue;

                GameObject instance = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    BaseEnemy instEnemy = instance.GetComponent<BaseEnemy>();
                    var serialized = new SerializedObject(instEnemy);
                    SerializedProperty prop = serialized.FindProperty("_tokenDropPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = tokenDrop;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(instance, path);
                        assignedCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }

            Debug.Log($"[EnemyDropSetupTool] Đã gán tham chiếu DB_TokenDrop cho {assignedCount} prefab enemy.");
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
