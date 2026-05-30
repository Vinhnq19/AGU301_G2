#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using DungeonBuilder.Building;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Enemy;
using DungeonBuilder.Enemy.Types;
using DungeonBuilder.Harvesting;
using DungeonBuilder.Networking;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Networking.Scopes;
using DungeonBuilder.Player;
using DungeonBuilder.Player.Tools;
using DungeonBuilder.UI.HUD;
using DungeonBuilder.Wave;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.Editor
{
    public static class DungeonBuilderBootstrapper
    {
        private const string DataRoot = "Assets/_Game/Generated/Data";
        private const string PrefabRoot = "Assets/_Game/Generated/Prefabs";
        private const string SpriteRoot = "Assets/_Game/Generated/Sprites";
        private const string NetworkPrefabsPath = "Assets/_Game/Generated/DB_NetworkPrefabs.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AutoRunFlagPath = "ProjectSettings/DungeonBuilderBootstrapRequested.flag";

        [InitializeOnLoadMethod]
        private static void BootstrapIfRequested()
        {
            if (!File.Exists(AutoRunFlagPath))
            {
                return;
            }

            File.Delete(AutoRunFlagPath);
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode)
                {
                    Bootstrap();
                }
            };
        }

        [MenuItem("Dungeon Builder/Bootstrap Generated Setup")]
        public static void BootstrapFromMenu()
        {
            Bootstrap();
        }

        public static void Bootstrap()
        {
            EnsureFolders();

            ShapeSprites sprites = CreateShapeSprites();
            GeneratedData data = CreateDataAssets();
            GeneratedPrefabs prefabs = CreatePrefabs(data, sprites);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CleanupLegacySceneObjects();

            SceneObjects sceneObjects = CreateSceneObjects(data, prefabs, sprites);
            ConfigureNetworkManager(prefabs);
            ConfigureDefaultNetworkPrefabs(prefabs);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Dungeon Builder generated setup complete.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game");
            EnsureFolder("Assets/_Game/Generated");
            EnsureFolder(DataRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder($"{PrefabRoot}/Enemies");
            EnsureFolder($"{PrefabRoot}/Harvesting");
            EnsureFolder($"{PrefabRoot}/Player");
            EnsureFolder($"{PrefabRoot}/Towers");
            EnsureFolder(SpriteRoot);
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

        private static ShapeSprites CreateShapeSprites()
        {
            return new ShapeSprites
            {
                Circle = CreateSprite("DB_Circle", ShapeKind.Circle, new Color32(255, 255, 255, 255)),
                Square = CreateSprite("DB_Square", ShapeKind.Square, new Color32(255, 255, 255, 255)),
                Capsule = CreateSprite("DB_Capsule", ShapeKind.Capsule, new Color32(255, 255, 255, 255))
            };
        }

        private static Sprite CreateSprite(string name, ShapeKind shapeKind, Color32 color)
        {
            string path = $"{SpriteRoot}/{name}.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var transparent = new Color32(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool filled = shapeKind switch
                    {
                        ShapeKind.Circle => IsInCircle(x, y, size),
                        ShapeKind.Capsule => IsInCapsule(x, y, size),
                        _ => x >= 8 && x < size - 8 && y >= 8 && y < size - 8
                    };
                    texture.SetPixel(x, y, filled ? color : transparent);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64f;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool IsInCircle(int x, int y, int size)
        {
            float dx = x - size * 0.5f;
            float dy = y - size * 0.5f;
            return dx * dx + dy * dy <= 24f * 24f;
        }

        private static bool IsInCapsule(int x, int y, int size)
        {
            const float radius = 18f;
            float centerX = size * 0.5f;
            float topY = 42f;
            float bottomY = 22f;

            if (y >= bottomY && y <= topY)
            {
                return Mathf.Abs(x - centerX) <= radius;
            }

            float capY = y > topY ? topY : bottomY;
            float dx = x - centerX;
            float dy = y - capY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static GeneratedData CreateDataAssets()
        {
            var data = new GeneratedData
            {
                Player = LoadOrCreate<PlayerDataSO>($"{DataRoot}/DB_PlayerData.asset"),
                Drone = LoadOrCreate<EnemyDataSO>($"{DataRoot}/DB_DroneData.asset"),
                Brute = LoadOrCreate<EnemyDataSO>($"{DataRoot}/DB_BruteData.asset"),
                MinerBug = LoadOrCreate<EnemyDataSO>($"{DataRoot}/DB_MinerBugData.asset"),
                ArrowTower = LoadOrCreate<TowerDataSO>($"{DataRoot}/DB_ArrowTowerData.asset"),
                CannonTower = LoadOrCreate<TowerDataSO>($"{DataRoot}/DB_CannonTowerData.asset"),
                FrostTower = LoadOrCreate<TowerDataSO>($"{DataRoot}/DB_FrostTowerData.asset"),
                WoodNode = LoadOrCreate<ResourceNodeDataSO>($"{DataRoot}/DB_WoodNodeData.asset"),
                StoneNode = LoadOrCreate<ResourceNodeDataSO>($"{DataRoot}/DB_StoneNodeData.asset"),
                OreNode = LoadOrCreate<ResourceNodeDataSO>($"{DataRoot}/DB_OreNodeData.asset"),
                CrystalNode = LoadOrCreate<ResourceNodeDataSO>($"{DataRoot}/DB_CrystalNodeData.asset")
            };

            data.Player.maxHP = 100f;
            data.Player.speed = 5f;
            data.Player.maxMana = 100f;
            data.Player.dashCooldown = 1f;

            ConfigureEnemy(data.Drone, EnemyType.Drone, 60f, 3f, 5);
            ConfigureEnemy(data.Brute, EnemyType.Brute, 160f, 1.4f, 15);
            ConfigureEnemy(data.MinerBug, EnemyType.MinerBug, 90f, 2.2f, 10);

            ConfigureTower(data.ArrowTower, TowerType.Arrow, 12f, 4f, 1.2f, 25, 0);
            ConfigureTower(data.CannonTower, TowerType.Cannon, 35f, 3.5f, 0.55f, 40, 15);
            ConfigureTower(data.FrostTower, TowerType.Frost, 6f, 3.8f, 0.8f, 25, 10);

            ConfigureResource(data.WoodNode, ResourceType.Wood, 4, 20, 100, 8f);
            ConfigureResource(data.StoneNode, ResourceType.Stone, 5, 15, 90, 10f);
            ConfigureResource(data.OreNode, ResourceType.Ore, 6, 10, 70, 12f);
            ConfigureResource(data.CrystalNode, ResourceType.Crystal, 8, 5, 50, 18f);

            MarkDirty(
                data.Player, data.Drone, data.Brute, data.MinerBug,
                data.ArrowTower, data.CannonTower, data.FrostTower,
                data.WoodNode, data.StoneNode, data.OreNode, data.CrystalNode);

            return data;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureEnemy(EnemyDataSO data, EnemyType type, float health, float speed, int reward)
        {
            data.enemyType = type;
            data.maxHealth = health;
            data.moveSpeed = speed;
            data.rewardGold = reward;
        }

        private static void ConfigureTower(TowerDataSO data, TowerType type, float damage, float range, float rate, int wood, int ore)
        {
            data.towerType = type;
            data.damage = damage;
            data.range = range;
            data.attackRate = rate;
            data.woodCost = wood;
            data.oreCost = ore;
        }

        private static void ConfigureResource(ResourceNodeDataSO data, ResourceType type, int hits, int amount, int max, float respawn)
        {
            data.resourceType = type;
            data.hitsToBreak = hits;
            data.amountPerHit = amount;
            data.maxAmount = max;
            data.respawnTime = respawn;
        }

        private static GeneratedPrefabs CreatePrefabs(GeneratedData data, ShapeSprites sprites)
        {
            var prefabs = new GeneratedPrefabs
            {
                Player = CreatePlayerPrefab(data.Player, sprites.Circle),
                ResourceDrop = CreateResourceDropPrefab(sprites.Circle)
            };

            prefabs.WoodNode = CreateResourceNodePrefab("DB_WoodNode", data.WoodNode, prefabs.ResourceDrop, sprites.Square, new Color(0.35f, 0.8f, 0.35f));
            prefabs.StoneNode = CreateResourceNodePrefab("DB_StoneNode", data.StoneNode, prefabs.ResourceDrop, sprites.Square, new Color(0.6f, 0.6f, 0.65f));
            prefabs.OreNode = CreateResourceNodePrefab("DB_OreNode", data.OreNode, prefabs.ResourceDrop, sprites.Square, new Color(0.35f, 0.45f, 0.75f));
            prefabs.CrystalNode = CreateResourceNodePrefab("DB_CrystalNode", data.CrystalNode, prefabs.ResourceDrop, sprites.Square, new Color(0.7f, 0.3f, 1f));

            prefabs.Drone = CreateEnemyPrefab<DroneEnemy>("DB_DroneEnemy", data.Drone, sprites.Circle, new Color(0.95f, 0.35f, 0.35f));
            prefabs.Brute = CreateEnemyPrefab<BruteEnemy>("DB_BruteEnemy", data.Brute, sprites.Capsule, new Color(0.8f, 0.2f, 0.15f));
            prefabs.MinerBug = CreateEnemyPrefab<MinerBugEnemy>("DB_MinerBugEnemy", data.MinerBug, sprites.Capsule, new Color(0.95f, 0.65f, 0.2f));

            prefabs.ArrowTower = CreateTowerPrefab("DB_ArrowTower", sprites.Square, new Color(0.2f, 0.65f, 1f));
            prefabs.CannonTower = CreateTowerPrefab("DB_CannonTower", sprites.Square, new Color(0.15f, 0.15f, 0.18f));
            prefabs.FrostTower = CreateTowerPrefab("DB_FrostTower", sprites.Square, new Color(0.45f, 0.9f, 1f));

            return prefabs;
        }

        private static NetworkObject CreatePlayerPrefab(PlayerDataSO playerData, Sprite circleSprite)
        {
            GameObject root = CreateNetworkPrefabRoot("DB_Player", includeNetworkTransform: true);
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
            root.AddComponent<CircleCollider2D>().radius = 0.42f;

            Transform visual = AddVisual(root, circleSprite, new Color(0.25f, 0.75f, 1f), Vector3.one);

            InputReader inputReader = root.AddComponent<InputReader>();
            PlayerController playerController = root.AddComponent<PlayerController>();
            PlayerStats playerStats = root.AddComponent<PlayerStats>();
            AxeTool axeTool = root.AddComponent<AxeTool>();
            PickaxeTool pickaxeTool = root.AddComponent<PickaxeTool>();
            WeaponTool weaponTool = root.AddComponent<WeaponTool>();
            BuilderTool builderTool = root.AddComponent<BuilderTool>();
            ToolController toolController = root.AddComponent<ToolController>();
            PlayerLifetimeScope lifetimeScope = root.AddComponent<PlayerLifetimeScope>();

            SetObject(inputReader, "_inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            SetObject(playerController, "_data", playerData);
            SetObject(playerStats, "_data", playerData);
            SetObjectArray(toolController, "_toolBehaviours", new Object[] { axeTool, pickaxeTool, weaponTool, builderTool });

            SetObject(lifetimeScope, "_inputReader", inputReader);
            SetObject(lifetimeScope, "_playerController", playerController);
            SetObject(lifetimeScope, "_playerStats", playerStats);
            SetObject(lifetimeScope, "_toolController", toolController);

            return SaveNetworkPrefab(root, $"{PrefabRoot}/Player/DB_Player.prefab");
        }

        private static NetworkObject CreateResourceDropPrefab(Sprite circleSprite)
        {
            GameObject root = CreateNetworkPrefabRoot("DB_ResourceDrop", includeNetworkTransform: true);
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            collider.isTrigger = true;

            Transform visual = AddVisual(root, circleSprite, new Color(1f, 0.85f, 0.25f), Vector3.one * 0.5f);
            ResourceDrop drop = root.AddComponent<ResourceDrop>();
            SetObject(drop, "_visual", visual);

            return SaveNetworkPrefab(root, $"{PrefabRoot}/Harvesting/DB_ResourceDrop.prefab");
        }

        private static NetworkObject CreateResourceNodePrefab(string name, ResourceNodeDataSO data, NetworkObject dropPrefab, Sprite sprite, Color color)
        {
            GameObject root = CreateNetworkPrefabRoot(name, includeNetworkTransform: false);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            Transform visual = AddVisual(root, sprite, color, Vector3.one);
            HarvestableNode node = root.AddComponent<HarvestableNode>();
            SetObject(node, "_data", data);
            SetObject(node, "_resourceDropPrefab", dropPrefab);
            SetObject(node, "_visual", visual);
            SetObjectArray(node, "_colliders", new Object[] { collider });

            return SaveNetworkPrefab(root, $"{PrefabRoot}/Harvesting/{name}.prefab");
        }

        private static NetworkObject CreateEnemyPrefab<TEnemy>(string name, EnemyDataSO data, Sprite sprite, Color color)
            where TEnemy : BaseEnemy
        {
            GameObject root = CreateNetworkPrefabRoot(name, includeNetworkTransform: true);
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
            root.AddComponent<CapsuleCollider2D>().size = new Vector2(0.8f, 1.2f);

            Transform visual = AddVisual(root, sprite, color, new Vector3(0.9f, 1.15f, 1f));
            TEnemy enemy = root.AddComponent<TEnemy>();
            SetObject(enemy, "_data", data);
            SetObject(enemy, "_visual", visual);

            return SaveNetworkPrefab(root, $"{PrefabRoot}/Enemies/{name}.prefab");
        }

        private static NetworkObject CreateTowerPrefab(string name, Sprite sprite, Color color)
        {
            GameObject root = CreateNetworkPrefabRoot(name, includeNetworkTransform: true);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            AddVisual(root, sprite, color, Vector3.one);
            return SaveNetworkPrefab(root, $"{PrefabRoot}/Towers/{name}.prefab");
        }

        private static GameObject CreateNetworkPrefabRoot(string name, bool includeNetworkTransform)
        {
            var root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            if (includeNetworkTransform)
            {
                root.AddComponent<NetworkTransform>();
            }

            return root;
        }

        private static Transform AddVisual(GameObject root, Sprite sprite, Color color, Vector3 scale)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = scale;

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 1;

            return visual.transform;
        }

        private static NetworkObject SaveNetworkPrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkObject>();
        }

        private static void CleanupLegacySceneObjects()
        {
            DeleteIfExists("Controller");
            DeleteIfExists("Player");
            DeleteIfExists("GameRoot");
            DeleteIfExists("DB_Core");
            DeleteIfExists("DB_HUDCanvas");
            DeleteIfExists("DB_ResourceNodes");
            DeleteIfExists("DB_SpawnPoints");
            DeleteIfExists("EventSystem");
        }

        private static void DeleteIfExists(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static SceneObjects CreateSceneObjects(GeneratedData data, GeneratedPrefabs prefabs, ShapeSprites sprites)
        {
            var sceneObjects = new SceneObjects();

            GameObject core = CreateSceneVisualObject("DB_Core", sprites.Square, new Color(0.1f, 0.9f, 0.85f), Vector3.zero, Vector3.one * 1.5f);
            sceneObjects.Core = core.transform;

            Transform spawnRoot = new GameObject("DB_SpawnPoints").transform;
            sceneObjects.SpawnPoints = new[]
            {
                CreateEmptyChild(spawnRoot, "Spawn_North", new Vector3(0f, 4f, 0f)),
                CreateEmptyChild(spawnRoot, "Spawn_East", new Vector3(5f, 0f, 0f)),
                CreateEmptyChild(spawnRoot, "Spawn_West", new Vector3(-5f, 0f, 0f))
            };

            Transform nodeRoot = new GameObject("DB_ResourceNodes").transform;
            InstantiateScenePrefab(prefabs.WoodNode, nodeRoot, "WoodNode", new Vector3(-4f, 2f, 0f));
            InstantiateScenePrefab(prefabs.StoneNode, nodeRoot, "StoneNode", new Vector3(-2f, 2.5f, 0f));
            InstantiateScenePrefab(prefabs.OreNode, nodeRoot, "OreNode", new Vector3(2f, 2.5f, 0f));
            InstantiateScenePrefab(prefabs.CrystalNode, nodeRoot, "CrystalNode", new Vector3(4f, 2f, 0f));

            GameObject gameRoot = new GameObject("GameRoot");
            gameRoot.AddComponent<NetworkObject>();
            NetworkObjectPool pool = gameRoot.AddComponent<NetworkObjectPool>();
            SharedResourceManager sharedResources = gameRoot.AddComponent<SharedResourceManager>();
            gameRoot.AddComponent<NetworkStatusDebugger>();
            GridManager grid = gameRoot.AddComponent<GridManager>();
            BuildingController building = gameRoot.AddComponent<BuildingController>();
            WaveManager wave = gameRoot.AddComponent<WaveManager>();
            GameLifetimeScope lifetimeScope = gameRoot.AddComponent<GameLifetimeScope>();

            Transform poolRoot = CreateEmptyChild(gameRoot.transform, "PoolRoot", Vector3.zero);
            ConfigurePool(pool, poolRoot, prefabs);
            ConfigureBuildingController(building, data, prefabs);
            ConfigureWaveManager(wave, sceneObjects.Core, sceneObjects.SpawnPoints, prefabs);

            HUDView hudView = CreateHUD();
            SetObject(lifetimeScope, "_networkObjectPool", pool);
            SetObject(lifetimeScope, "_sharedResourceManager", sharedResources);
            SetObject(lifetimeScope, "_gridManager", grid);
            SetObject(lifetimeScope, "_buildingController", building);
            SetObject(lifetimeScope, "_waveManager", wave);
            SetObject(lifetimeScope, "_hudView", hudView);

            CreateEventSystem();

            sceneObjects.GameRoot = gameRoot.transform;
            return sceneObjects;
        }

        private static GameObject CreateSceneVisualObject(string name, Sprite sprite, Color color, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = scale;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return go;
        }

        private static Transform CreateEmptyChild(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        private static void InstantiateScenePrefab(NetworkObject prefab, Transform parent, string name, Vector3 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
        }

        private static void ConfigurePool(NetworkObjectPool pool, Transform poolRoot, GeneratedPrefabs prefabs)
        {
            SetPoolEntries(pool, new[]
            {
                new PoolConfig(prefabs.ResourceDrop, 12, poolRoot),
                new PoolConfig(prefabs.Drone, 12, poolRoot),
                new PoolConfig(prefabs.Brute, 6, poolRoot),
                new PoolConfig(prefabs.MinerBug, 8, poolRoot),
                new PoolConfig(prefabs.ArrowTower, 8, poolRoot),
                new PoolConfig(prefabs.CannonTower, 4, poolRoot),
                new PoolConfig(prefabs.FrostTower, 4, poolRoot)
            });
        }

        private static void ConfigureBuildingController(BuildingController building, GeneratedData data, GeneratedPrefabs prefabs)
        {
            SetObjectArray(building, "_towerData", new Object[] { data.ArrowTower, data.CannonTower, data.FrostTower });

            SerializedObject serialized = new SerializedObject(building);
            SerializedProperty entries = serialized.FindProperty("_towerPrefabs");
            entries.arraySize = 3;
            SetTowerPrefabEntry(entries.GetArrayElementAtIndex(0), TowerType.Arrow, prefabs.ArrowTower);
            SetTowerPrefabEntry(entries.GetArrayElementAtIndex(1), TowerType.Cannon, prefabs.CannonTower);
            SetTowerPrefabEntry(entries.GetArrayElementAtIndex(2), TowerType.Frost, prefabs.FrostTower);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(building);
        }

        private static void SetTowerPrefabEntry(SerializedProperty entry, TowerType type, NetworkObject prefab)
        {
            entry.FindPropertyRelative("_towerType").enumValueIndex = (int)type;
            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
        }

        private static void ConfigureWaveManager(WaveManager wave, Transform core, Transform[] spawnPoints, GeneratedPrefabs prefabs)
        {
            SetObject(wave, "_coreTarget", core);
            SetObjectArray(wave, "_spawnPoints", spawnPoints);
            SetObjectArray(wave, "_enemyPrefabs", new Object[] { prefabs.Drone, prefabs.Brute, prefabs.MinerBug });
        }

        private static void SetPoolEntries(NetworkObjectPool pool, IReadOnlyList<PoolConfig> configs)
        {
            SerializedObject serialized = new SerializedObject(pool);
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = configs.Count;

            for (int i = 0; i < configs.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_prefab").objectReferenceValue = configs[i].Prefab;
                entry.FindPropertyRelative("_prewarmCount").intValue = configs[i].PrewarmCount;
                entry.FindPropertyRelative("_parent").objectReferenceValue = configs[i].Parent;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pool);
        }

        private static HUDView CreateHUD()
        {
            var canvasGo = new GameObject("DB_HUDCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            HUDView view = canvasGo.AddComponent<HUDView>();

            TMP_Text wood = CreateText(canvasGo.transform, "WoodText", "Wood: 0", new Vector2(90f, -30f));
            TMP_Text stone = CreateText(canvasGo.transform, "StoneText", "Stone: 0", new Vector2(90f, -60f));
            TMP_Text ore = CreateText(canvasGo.transform, "OreText", "Ore: 0", new Vector2(90f, -90f));
            TMP_Text crystal = CreateText(canvasGo.transform, "CrystalText", "Crystal: 0", new Vector2(90f, -120f));
            TMP_Text wave = CreateText(canvasGo.transform, "WaveText", "Wave: 0", new Vector2(-90f, -30f), TextAnchor.UpperRight);
            TMP_Text countdown = CreateText(canvasGo.transform, "CountdownText", "30", new Vector2(-90f, -60f), TextAnchor.UpperRight);
            TMP_Text core = CreateText(canvasGo.transform, "CoreHealthText", "Core: 100", new Vector2(-90f, -90f), TextAnchor.UpperRight);

            SetObject(view, "_woodText", wood);
            SetObject(view, "_stoneText", stone);
            SetObject(view, "_oreText", ore);
            SetObject(view, "_crystalText", crystal);
            SetObject(view, "_waveText", wave);
            SetObject(view, "_countdownText", countdown);
            SetObject(view, "_coreHealthText", core);

            return view;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 anchoredPosition, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor == TextAnchor.UpperRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = anchor == TextAnchor.UpperRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(180f, 28f);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = 18f;
            text.alignment = anchor == TextAnchor.UpperRight ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void ConfigureNetworkManager(GeneratedPrefabs prefabs)
        {
            NetworkManager networkManager = Object.FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                GameObject go = new GameObject("NetworkManager");
                networkManager = go.AddComponent<NetworkManager>();
                go.AddComponent<UnityTransport>();
            }

            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
            }

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = prefabs.Player.gameObject;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(CreateNetworkPrefabsList(prefabs));
            EditorUtility.SetDirty(networkManager);
        }

        private static NetworkPrefabsList CreateNetworkPrefabsList(GeneratedPrefabs prefabs)
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            }

            SerializedObject serialized = new SerializedObject(list);
            serialized.FindProperty("List").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AddNetworkPrefab(list, prefabs.Player);
            AddNetworkPrefab(list, prefabs.ResourceDrop);
            AddNetworkPrefab(list, prefabs.Drone);
            AddNetworkPrefab(list, prefabs.Brute);
            AddNetworkPrefab(list, prefabs.MinerBug);
            AddNetworkPrefab(list, prefabs.ArrowTower);
            AddNetworkPrefab(list, prefabs.CannonTower);
            AddNetworkPrefab(list, prefabs.FrostTower);

            EditorUtility.SetDirty(list);
            return list;
        }

        private static void AddNetworkPrefab(NetworkPrefabsList list, NetworkObject prefab)
        {
            list.Add(new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = prefab.gameObject
            });
        }

        private static void ConfigureDefaultNetworkPrefabs(GeneratedPrefabs prefabs)
        {
            NetworkPrefabsList defaultList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (defaultList == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(defaultList);
            serialized.FindProperty("List").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AddNetworkPrefab(defaultList, prefabs.Player);
            AddNetworkPrefab(defaultList, prefabs.ResourceDrop);
            AddNetworkPrefab(defaultList, prefabs.Drone);
            AddNetworkPrefab(defaultList, prefabs.Brute);
            AddNetworkPrefab(defaultList, prefabs.MinerBug);
            AddNetworkPrefab(defaultList, prefabs.ArrowTower);
            AddNetworkPrefab(defaultList, prefabs.CannonTower);
            AddNetworkPrefab(defaultList, prefabs.FrostTower);

            EditorUtility.SetDirty(defaultList);
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Could not find serialized property {propertyName} on {target.name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectArray(Object target, string propertyName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Could not find serialized property {propertyName} on {target.name}.");
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

        private static void MarkDirty(params Object[] assets)
        {
            foreach (Object asset in assets)
            {
                EditorUtility.SetDirty(asset);
            }
        }

        private enum ShapeKind
        {
            Circle,
            Square,
            Capsule
        }

        private readonly struct PoolConfig
        {
            public readonly NetworkObject Prefab;
            public readonly int PrewarmCount;
            public readonly Transform Parent;

            public PoolConfig(NetworkObject prefab, int prewarmCount, Transform parent)
            {
                Prefab = prefab;
                PrewarmCount = prewarmCount;
                Parent = parent;
            }
        }

        private sealed class ShapeSprites
        {
            public Sprite Circle;
            public Sprite Square;
            public Sprite Capsule;
        }

        private sealed class GeneratedData
        {
            public PlayerDataSO Player;
            public EnemyDataSO Drone;
            public EnemyDataSO Brute;
            public EnemyDataSO MinerBug;
            public TowerDataSO ArrowTower;
            public TowerDataSO CannonTower;
            public TowerDataSO FrostTower;
            public ResourceNodeDataSO WoodNode;
            public ResourceNodeDataSO StoneNode;
            public ResourceNodeDataSO OreNode;
            public ResourceNodeDataSO CrystalNode;
        }

        private sealed class GeneratedPrefabs
        {
            public NetworkObject Player;
            public NetworkObject ResourceDrop;
            public NetworkObject WoodNode;
            public NetworkObject StoneNode;
            public NetworkObject OreNode;
            public NetworkObject CrystalNode;
            public NetworkObject Drone;
            public NetworkObject Brute;
            public NetworkObject MinerBug;
            public NetworkObject ArrowTower;
            public NetworkObject CannonTower;
            public NetworkObject FrostTower;
        }

        private sealed class SceneObjects
        {
            public Transform GameRoot;
            public Transform Core;
            public Transform[] SpawnPoints;
        }
    }
}
#endif
