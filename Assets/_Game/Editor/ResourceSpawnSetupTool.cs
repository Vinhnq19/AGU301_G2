#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Harvesting;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Networking.Scopes;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Tool tự động setup hệ thống sinh resource theo wave:
    /// - Sinh shader flash (URP 2D Sprite) + material.
    /// - Tạo các ScriptableObject (ResourceSpawnConfig, node data còn thiếu).
    /// - Drop riêng từng loại do tool "Setup Per-Type Resource Drops" lo (ResourceDropSetupTool).
    /// - Tạo/wire GameObject ResourceSpawner + slot trong SampleScene.
    /// - Gán _visualRenderer + material flash cho các node prefab và đăng ký vào pool + NetworkPrefabs.
    ///
    /// Tool idempotent: chạy nhiều lần không tạo trùng (LoadOrCreate).
    /// </summary>
    public static class ResourceSpawnSetupTool
    {
        private const string DataRoot = "Assets/_Game/Generated/Data";
        private const string ResourceDataRoot = "Assets/_Game/Generated/Data/ResourceData";
        private const string PrefabRoot = "Assets/_Game/Generated/Prefabs";
        private const string ShaderPath = "Assets/_Game/Material/SpriteFlash.shader";
        private const string FlashMaterialPath = "Assets/_Game/Material/M_SpriteFlash.mat";
        private const string SpawnConfigPath = "Assets/_Game/Generated/Data/ResourceData/DB_ResourceSpawnConfig.asset";
        private const string NetworkPrefabsPath = "Assets/_Game/Generated/DB_NetworkPrefabs.asset";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private const int SlotCount = 10;

        [MenuItem("Dungeon Builder/Setup Resource Spawn System")]
        public static void Setup()
        {
            EnsureFolders();

            Shader flashShader = GenerateFlashShader();
            Material flashMaterial = CreateFlashMaterial(flashShader);

            // Node prefab + data theo loại tài nguyên muốn spawn theo wave.
            var defs = BuildResourceDefinitions();
            EnsureNodeData(defs);
            EnsureNodePrefabs(defs);

            ResourceSpawnConfigSO config = CreateSpawnConfig(defs);

            // Gán material flash + _visualRenderer cho prefab node hiện có.
            foreach (ResourceDef def in defs)
            {
                ApplyFlashToNodePrefab(def, flashMaterial);
            }

            RegisterNodePrefabsInPoolAndNetwork(defs);

            CreateSpawnerInScene(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ResourceSpawnSetupTool] Setup hoàn tất. Kiểm tra ResourceSpawner trong SampleScene và DB_ResourceSpawnConfig.");
        }

        // ----------------------------------------------------------------------------------
        // Shader + material
        // ----------------------------------------------------------------------------------

        private static Shader GenerateFlashShader()
        {
            if (!File.Exists(ShaderPath))
            {
                File.WriteAllText(ShaderPath, FlashShaderSource(), Encoding.UTF8);
                AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceUpdate);
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[ResourceSpawnSetupTool] Không load được shader tại {ShaderPath}.");
            }

            return shader;
        }

        private static Material CreateFlashMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (material == null && shader != null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, FlashMaterialPath);
            }
            else if (material != null && shader != null && material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            if (material != null)
            {
                material.SetColor("_FlashColor", Color.white);
                material.SetFloat("_FlashAmount", 0f);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>
        /// Shader URP 2D Sprite (unlit) với property _FlashAmount/_FlashColor để C# lerp màu khi flash.
        /// Hỗ trợ MaterialPropertyBlock (per-renderer override) nên một material dùng chung cho mọi node.
        /// </summary>
        private static string FlashShaderSource()
        {
            return @"Shader ""Dungeon Builder/SpriteFlash""
{
    Properties
    {
        [PerRendererData] _MainTex (""Sprite Texture"", 2D) = ""white"" {}
        _Color (""Tint"", Color) = (1,1,1,1)
        _FlashColor (""Flash Color"", Color) = (1,1,1,1)
        _FlashAmount (""Flash Amount"", Range(0,1)) = 0
        [HideInInspector] _RendererColor (""RendererColor"", Color) = (1,1,1,1)
        [HideInInspector] _Flip (""Flip"", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            ""Queue"" = ""Transparent""
            ""RenderType"" = ""Transparent""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""IgnoreProjector"" = ""True""
            ""PreviewType"" = ""Plane""
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { ""LightMode"" = ""Universal2D"" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FlashColor;
                float _FlashAmount;
                float4 _RendererColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color * _RendererColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 c = tex * IN.color;
                // Lerp RGB sang flash color theo _FlashAmount, giữ nguyên alpha gốc.
                c.rgb = lerp(c.rgb, _FlashColor.rgb, saturate(_FlashAmount));
                return c;
            }
            ENDHLSL
        }
    }

    Fallback ""Sprites/Default""
}
";
        }

        // ----------------------------------------------------------------------------------
        // Resource definitions + data
        // ----------------------------------------------------------------------------------

        private struct ResourceDef
        {
            public ResourceType Type;
            public string NodePrefabPath;   // có thể null nếu chưa có prefab
            public string TemplatePrefabPath; // prefab nguồn để clone nếu NodePrefabPath chưa tồn tại
            public string NodeDataPath;
            public int MinWaveToAppear;
            public float BaseWeight;
            public float WeightGainPerWave;
            public float MaxWeight;
            public Color IconColor;
            // data
            public int HitsToBreak;
            public int AmountPerHit;
            public int MaxAmount;
        }

        private static List<ResourceDef> BuildResourceDefinitions()
        {
            // Mapping prefab dựa trên prefab có sẵn do bootstrapper sinh ra.
            return new List<ResourceDef>
            {
                new ResourceDef
                {
                    Type = ResourceType.Wood,
                    NodePrefabPath = $"{PrefabRoot}/Harvesting/DB_WoodNode.prefab",
                    NodeDataPath = $"{DataRoot}/DB_WoodNodeData.asset",
                    MinWaveToAppear = 1, BaseWeight = 10f, WeightGainPerWave = 0f, MaxWeight = 10f,
                    IconColor = new Color(0.35f, 0.8f, 0.35f),
                    HitsToBreak = 4, AmountPerHit = 20, MaxAmount = 100
                },
                new ResourceDef
                {
                    Type = ResourceType.Stone,
                    NodePrefabPath = $"{PrefabRoot}/Harvesting/DB_StoneNode.prefab",
                    NodeDataPath = $"{DataRoot}/DB_StoneNodeData.asset",
                    MinWaveToAppear = 1, BaseWeight = 8f, WeightGainPerWave = 0f, MaxWeight = 8f,
                    IconColor = new Color(0.6f, 0.6f, 0.65f),
                    HitsToBreak = 5, AmountPerHit = 15, MaxAmount = 90
                },
                new ResourceDef
                {
                    Type = ResourceType.Ore,
                    NodePrefabPath = $"{PrefabRoot}/Harvesting/DB_OreNode.prefab",
                    NodeDataPath = $"{DataRoot}/DB_OreNodeData.asset",
                    MinWaveToAppear = 2, BaseWeight = 4f, WeightGainPerWave = 0.3f, MaxWeight = 8f,
                    IconColor = new Color(0.35f, 0.45f, 0.75f),
                    HitsToBreak = 6, AmountPerHit = 10, MaxAmount = 70
                },
                new ResourceDef
                {
                    Type = ResourceType.Crystal,
                    NodePrefabPath = $"{PrefabRoot}/Harvesting/DB_CrystalNode.prefab",
                    NodeDataPath = $"{DataRoot}/DB_CrystalNodeData.asset",
                    MinWaveToAppear = 3, BaseWeight = 2f, WeightGainPerWave = 0.4f, MaxWeight = 7f,
                    IconColor = new Color(0.7f, 0.3f, 1f),
                    HitsToBreak = 8, AmountPerHit = 5, MaxAmount = 50
                },
                new ResourceDef
                {
                    Type = ResourceType.BlueGems,
                    NodePrefabPath = $"{PrefabRoot}/Harvesting/DB_BlueGemNode.prefab",
                    TemplatePrefabPath = $"{PrefabRoot}/Harvesting/DB_CrystalNode.prefab",
                    NodeDataPath = $"{ResourceDataRoot}/DB_BlueGemNodeData.asset",
                    MinWaveToAppear = 5, BaseWeight = 1f, WeightGainPerWave = 0.6f, MaxWeight = 6f,
                    IconColor = new Color(0.3f, 0.5f, 1f),
                    HitsToBreak = 10, AmountPerHit = 3, MaxAmount = 30
                }
            };
        }

        private static void EnsureNodeData(List<ResourceDef> defs)
        {
            foreach (ResourceDef def in defs)
            {
                ResourceNodeDataSO data = AssetDatabase.LoadAssetAtPath<ResourceNodeDataSO>(def.NodeDataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<ResourceNodeDataSO>();
                    AssetDatabase.CreateAsset(data, def.NodeDataPath);
                }

                data.resourceType = def.Type;
                data.hitsToBreak = def.HitsToBreak;
                data.amountPerHit = def.AmountPerHit;
                data.maxAmount = def.MaxAmount;
                if (data.respawnTime <= 0f) data.respawnTime = 10f;
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>
        /// Clone prefab template cho loại chưa có prefab (vd BlueGems clone từ Crystal),
        /// gán data + đổi màu visual để phân biệt.
        /// </summary>
        private static void EnsureNodePrefabs(List<ResourceDef> defs)
        {
            foreach (ResourceDef def in defs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(def.NodePrefabPath) != null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(def.TemplatePrefabPath)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(def.TemplatePrefabPath) == null)
                {
                    continue;
                }

                if (!AssetDatabase.CopyAsset(def.TemplatePrefabPath, def.NodePrefabPath))
                {
                    Debug.LogWarning($"[ResourceSpawnSetupTool] Không clone được prefab từ {def.TemplatePrefabPath} sang {def.NodePrefabPath}.");
                    continue;
                }

                AssetDatabase.ImportAsset(def.NodePrefabPath, ImportAssetOptions.ForceUpdate);

                GameObject instance = PrefabUtility.LoadPrefabContents(def.NodePrefabPath);
                try
                {
                    HarvestableNode node = instance.GetComponent<HarvestableNode>();
                    ResourceNodeDataSO data = AssetDatabase.LoadAssetAtPath<ResourceNodeDataSO>(def.NodeDataPath);
                    if (node != null && data != null)
                    {
                        SetObject(node, "_data", data);
                    }

                    SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer != null)
                    {
                        renderer.color = def.IconColor;
                    }

                    PrefabUtility.SaveAsPrefabAsset(instance, def.NodePrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
        }

        private static ResourceSpawnConfigSO CreateSpawnConfig(List<ResourceDef> defs)
        {
            ResourceSpawnConfigSO config = AssetDatabase.LoadAssetAtPath<ResourceSpawnConfigSO>(SpawnConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ResourceSpawnConfigSO>();
                AssetDatabase.CreateAsset(config, SpawnConfigPath);
            }

            config.baseNodesPerWave = 2;
            config.nodesPerWaveGain = 1f;
            config.maxNodesPerWave = 12;
            config.entries = new List<ResourceSpawnEntry>();

            foreach (ResourceDef def in defs)
            {
                NetworkObject prefab = LoadNodePrefab(def.NodePrefabPath);
                ResourceNodeDataSO data = AssetDatabase.LoadAssetAtPath<ResourceNodeDataSO>(def.NodeDataPath);

                config.entries.Add(new ResourceSpawnEntry
                {
                    resourceType = def.Type,
                    nodePrefab = prefab,
                    nodeData = data,
                    minWaveToAppear = def.MinWaveToAppear,
                    baseWeight = def.BaseWeight,
                    weightGainPerWave = def.WeightGainPerWave,
                    maxWeight = def.MaxWeight
                });

                if (prefab == null)
                {
                    Debug.LogWarning($"[ResourceSpawnSetupTool] Chưa có prefab cho {def.Type} tại {def.NodePrefabPath}. Entry vẫn được tạo, hãy gán prefab sau (vd nhân bản DB_CrystalNode cho BlueGems).");
                }
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        // ----------------------------------------------------------------------------------
        // Node prefab wiring
        // ----------------------------------------------------------------------------------

        private static NetworkObject LoadNodePrefab(string path)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null ? go.GetComponent<NetworkObject>() : null;
        }

        private static void ApplyFlashToNodePrefab(ResourceDef def, Material flashMaterial)
        {
            if (flashMaterial == null || AssetDatabase.LoadAssetAtPath<GameObject>(def.NodePrefabPath) == null)
            {
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(def.NodePrefabPath);
            try
            {
                HarvestableNode node = instance.GetComponent<HarvestableNode>();
                if (node == null)
                {
                    return;
                }

                // Tìm SpriteRenderer của child Visual.
                SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.sharedMaterial = flashMaterial;
                }

                SetObject(node, "_visualRenderer", renderer);

                PrefabUtility.SaveAsPrefabAsset(instance, def.NodePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static void RegisterNodePrefabsInPoolAndNetwork(List<ResourceDef> defs)
        {
            Scene scene = GetOrOpenScene();

            NetworkObjectPool pool = Object.FindFirstObjectByType<NetworkObjectPool>();
            Transform poolRoot = pool != null ? pool.transform.Find("PoolRoot") : null;
            if (poolRoot == null && pool != null) poolRoot = pool.transform;

            var nodePrefabs = new List<NetworkObject>();
            foreach (ResourceDef def in defs)
            {
                NetworkObject prefab = LoadNodePrefab(def.NodePrefabPath);
                if (prefab != null)
                {
                    nodePrefabs.Add(prefab);
                }
            }

            if (pool != null)
            {
                AppendPoolEntries(pool, nodePrefabs, poolRoot, prewarmCount: 4);
            }
            else
            {
                Debug.LogWarning("[ResourceSpawnSetupTool] Không tìm thấy NetworkObjectPool trong scene để đăng ký node prefab.");
            }

            RegisterNetworkPrefabs(nodePrefabs);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AppendPoolEntries(NetworkObjectPool pool, List<NetworkObject> prefabs, Transform parent, int prewarmCount)
        {
            var serialized = new SerializedObject(pool);
            SerializedProperty entries = serialized.FindProperty("_entries");

            // Thu thập prefab đã có để tránh trùng.
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
                Debug.LogWarning($"[ResourceSpawnSetupTool] Không tìm thấy NetworkPrefabsList tại {NetworkPrefabsPath}. Hãy thêm node prefab vào NetworkPrefabs thủ công.");
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

        // ----------------------------------------------------------------------------------
        // Scene: ResourceSpawner + slots + wiring
        // ----------------------------------------------------------------------------------

        private static void CreateSpawnerInScene(ResourceSpawnConfigSO config)
        {
            Scene scene = GetOrOpenScene();

            ResourceSpawner spawner = Object.FindFirstObjectByType<ResourceSpawner>();
            GameObject spawnerGo;
            if (spawner == null)
            {
                spawnerGo = new GameObject("DB_ResourceSpawner");
                spawnerGo.AddComponent<NetworkObject>();
                spawner = spawnerGo.AddComponent<ResourceSpawner>();
            }
            else
            {
                spawnerGo = spawner.gameObject;
            }

            // Slots: tạo root + các empty con nếu chưa có.
            Transform slotRoot = spawnerGo.transform.Find("Slots");
            if (slotRoot == null)
            {
                slotRoot = new GameObject("Slots").transform;
                slotRoot.SetParent(spawnerGo.transform, false);
            }

            var slots = new List<Transform>();
            for (int i = 0; i < SlotCount; i++)
            {
                string slotName = $"Slot_{i:00}";
                Transform slot = slotRoot.Find(slotName);
                if (slot == null)
                {
                    slot = new GameObject(slotName).transform;
                    slot.SetParent(slotRoot, false);
                    // Rải slot theo lưới quanh gốc.
                    float x = (i % 5) * 2f - 4f;
                    float y = (i / 5) * 2f + 2f;
                    slot.position = new Vector3(x, y, 0f);
                }

                slots.Add(slot);
            }

            SetObject(spawner, "_config", config);
            SetObjectArray(spawner, "_spawnSlots", slots.ToArray());

            // Wire vào GameLifetimeScope.
            GameLifetimeScope lifetimeScope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (lifetimeScope != null)
            {
                SetObject(lifetimeScope, "_resourceSpawner", spawner);
            }
            else
            {
                Debug.LogWarning("[ResourceSpawnSetupTool] Không tìm thấy GameLifetimeScope để wire ResourceSpawner.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ----------------------------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------------------------

        private static Scene GetOrOpenScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == ScenePath && active.isLoaded)
            {
                return active;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/Material");
            EnsureFolder(DataRoot);
            EnsureFolder(ResourceDataRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[ResourceSpawnSetupTool] Không tìm thấy property {propertyName} trên {target.name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectArray(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[ResourceSpawnSetupTool] Không tìm thấy property {propertyName} trên {target.name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
