using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Building;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using DungeonBuilder.Networking;
using DungeonBuilder.Networking.Scopes;
using DungeonBuilder.UI.TowerSelection;
using DungeonBuilder.Wave;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace DungeonBuilder.Tests
{
    public sealed class BuildCommandValidatorTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object target in _objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void Placement_RejectsCombatMissingSenderRangeAndInvalidGrid()
        {
            GridManager grid = CreateComponent<GridManager>("Grid");
            BuildAuthoritySettingsSO settings = CreateScriptableObject<BuildAuthoritySettingsSO>();
            var phase = new FakePhaseProvider { CurrentPhase = GamePhase.Combat };
            var resolver = new FakeEntityResolver();
            var validator = new BuildCommandValidator(phase, resolver, grid, settings);

            Assert.That(validator.ValidatePlacement(1, Vector2Int.zero).Code, Is.EqualTo(BuildValidationCode.NotBuildPhase));

            phase.CurrentPhase = GamePhase.Build;
            Assert.That(validator.ValidatePlacement(1, Vector2Int.zero).Code, Is.EqualTo(BuildValidationCode.SenderPlayerNotFound));

            NetworkObject player = CreateNetworkObject("Player", new Vector3(7f, 0f, 0f));
            resolver.Players[1] = player;
            Assert.That(validator.ValidatePlacement(1, Vector2Int.zero).Code, Is.EqualTo(BuildValidationCode.OutOfRange));

            player.transform.position = Vector3.zero;
            Assert.That(validator.ValidatePlacement(1, new Vector2Int(100, 100)).Code, Is.EqualTo(BuildValidationCode.InvalidGridPosition));
            Assert.That(validator.ValidatePlacement(1, Vector2Int.zero).IsAllowed, Is.True);
        }

        [Test]
        public void TowerAction_RequiresRegisteredTowerAndRange()
        {
            GridManager grid = CreateComponent<GridManager>("Grid");
            TowerDataSO towerData = CreateScriptableObject<TowerDataSO>();
            BuildAuthoritySettingsSO settings = CreateScriptableObject<BuildAuthoritySettingsSO>();
            var phase = new FakePhaseProvider { CurrentPhase = GamePhase.Build };
            var resolver = new FakeEntityResolver();
            NetworkObject player = CreateNetworkObject("Player", Vector3.zero);
            resolver.Players[2] = player;
            var validator = new BuildCommandValidator(phase, resolver, grid, settings);

            Assert.That(
                validator.ValidateTowerAction(2, Vector2Int.zero, out _).Code,
                Is.EqualTo(BuildValidationCode.TowerNotFound));

            Assert.That(grid.PlaceTower(Vector2Int.zero, towerData), Is.True);
            grid.SetTowerNetworkObjectId(Vector2Int.zero, 25);
            Assert.That(
                validator.ValidateTowerAction(2, Vector2Int.zero, out _).Code,
                Is.EqualTo(BuildValidationCode.TowerNotFound));

            NetworkObject tower = CreateNetworkObject("Tower", new Vector3(5f, 0f, 0f));
            resolver.SpawnedObjects[25] = tower;
            Assert.That(validator.ValidateTowerAction(2, Vector2Int.zero, out NetworkObject resolved).IsAllowed, Is.True);
            Assert.That(resolved, Is.SameAs(tower));

            tower.transform.position = new Vector3(7f, 0f, 0f);
            Assert.That(
                validator.ValidateTowerAction(2, Vector2Int.zero, out _).Code,
                Is.EqualTo(BuildValidationCode.OutOfRange));
        }

        [Test]
        public void Container_ResolvesAuthorityServices()
        {
            GridManager grid = CreateComponent<GridManager>("Grid");
            BuildAuthoritySettingsSO settings = CreateScriptableObject<BuildAuthoritySettingsSO>();
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IGamePhaseProvider>(new FakePhaseProvider());
            builder.RegisterInstance<INetworkEntityResolver>(new FakeEntityResolver());
            builder.RegisterInstance(grid);
            builder.RegisterInstance(settings);
            builder.Register<NetworkEntityResolver>(Lifetime.Singleton).AsSelf();
            builder.Register<BuildCommandValidator>(Lifetime.Singleton).As<IBuildCommandValidator>();

            using IObjectResolver container = builder.Build();

            Assert.That(container.Resolve<IBuildCommandValidator>(), Is.Not.Null);
            Assert.That(container.Resolve<NetworkEntityResolver>(), Is.Not.Null);
        }

        [Test]
        public void TowerSelectionPresenter_InitializeAndDispose_DoNotDuplicateResourceSubscription()
        {
            TowerSelectionView view = CreateComponent<TowerSelectionView>("TowerSelectionView");
            var model = new TowerSelectionModel();
            var resources = new TrackingResourceService();
            var presenter = new TowerSelectionPresenter(view, model, null, resources, null);

            Assert.That(resources.SubscriptionCount, Is.EqualTo(1));
            presenter.Initialize();
            presenter.Initialize();
            Assert.That(resources.SubscriptionCount, Is.EqualTo(1));

            presenter.Dispose();
            presenter.Dispose();
            Assert.That(resources.SubscriptionCount, Is.Zero);
        }

        [Test]
        public void SampleScene_HasAuthoritySettingsAssigned()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameLifetimeScope scope = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    scope = root.GetComponentInChildren<GameLifetimeScope>(true);
                    if (scope != null)
                    {
                        break;
                    }
                }

                Assert.That(scope, Is.Not.Null);
                var serializedScope = new SerializedObject(scope);
                Assert.That(
                    serializedScope.FindProperty("_buildAuthoritySettings").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serializedScope.FindProperty("_towerSelectionView").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serializedScope.FindProperty("_towerCatalog").objectReferenceValue,
                    Is.Not.Null);

                WaveManager waveManager = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    waveManager = root.GetComponentInChildren<WaveManager>(true);
                    if (waveManager != null)
                    {
                        break;
                    }
                }

                Assert.That(waveManager, Is.Not.Null);
                var serializedWaveManager = new SerializedObject(waveManager);
                Assert.That(
                    serializedWaveManager.FindProperty("_waveCatalog").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serializedWaveManager.FindProperty("_enemyPrefabMappings").arraySize,
                    Is.EqualTo(3));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<BuildAuthoritySettingsSO>(
                        "Assets/_Game/Generated/Data/DB_BuildAuthoritySettings.asset"),
                    Is.Not.Null);
            }
            finally
            {
                if (openedByTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private NetworkObject CreateNetworkObject(string name, Vector3 position)
        {
            NetworkObject networkObject = CreateComponent<NetworkObject>(name);
            networkObject.transform.position = position;
            return networkObject;
        }

        private T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _objects.Add(asset);
            return asset;
        }

        private sealed class FakePhaseProvider : IGamePhaseProvider
        {
            public GamePhase CurrentPhase { get; set; } = GamePhase.Build;
        }

        private sealed class FakeEntityResolver : INetworkEntityResolver
        {
            public readonly Dictionary<ulong, NetworkObject> Players = new();
            public readonly Dictionary<ulong, NetworkObject> SpawnedObjects = new();

            public bool TryGetPlayerObject(ulong clientId, out NetworkObject playerObject)
            {
                return Players.TryGetValue(clientId, out playerObject);
            }

            public bool TryGetSpawnedObject(ulong networkObjectId, out NetworkObject networkObject)
            {
                return SpawnedObjects.TryGetValue(networkObjectId, out networkObject);
            }
        }

        private sealed class TrackingResourceService : IResourceService
        {
            private Action<ResourceChanged> _resourceChanged;

            public int SubscriptionCount { get; private set; }

            public event Action<ResourceChanged> ResourceChanged
            {
                add
                {
                    _resourceChanged += value;
                    SubscriptionCount++;
                }
                remove
                {
                    _resourceChanged -= value;
                    SubscriptionCount--;
                }
            }

            public int GetAmount(ResourceType type) => 0;
            public IReadOnlyDictionary<ResourceType, int> GetSnapshot() => new Dictionary<ResourceType, int>();
            public bool CanAfford(IReadOnlyList<ResourceCost> costs) => false;
            public bool TrySet(ResourceType type, int amount) => false;
            public bool TryAdd(ResourceType type, int amount) => false;
            public bool TrySpend(IReadOnlyList<ResourceCost> costs) => false;
            public bool TryReset(ResourceType type) => false;
            public bool TryResetAll() => false;
        }
    }
}
