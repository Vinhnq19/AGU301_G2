using DungeonBuilder.Building;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using DungeonBuilder.Harvesting;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.UI.HUD;
using DungeonBuilder.UI.TowerSelection;
using DungeonBuilder.Wave;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DungeonBuilder.Networking.Scopes
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Scene Services")]
        [SerializeField] private NetworkObjectPool _networkObjectPool;
        [SerializeField] private SharedResourceManager _sharedResourceManager;
        [SerializeField] private CoreManager _coreManager;
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private BuildingController _buildingController;
        [SerializeField] private WaveManager _waveManager;

        [Header("UI")]
        [SerializeField] private HUDView _hudView;
        [SerializeField] private TowerSelectionView _towerSelectionView;

        [Header("Data")]
        [SerializeField] private TowerCatalogSO _towerCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<EventBus>(Lifetime.Singleton);
            builder.Register<HUDModel>(Lifetime.Singleton);
            builder.RegisterEntryPoint<HUDPresenter>(Lifetime.Singleton);

            if (_networkObjectPool != null)
            {
                builder.RegisterComponent(_networkObjectPool).AsImplementedInterfaces();
            }

            if (_sharedResourceManager != null)
            {
                builder.RegisterComponent(_sharedResourceManager).As<IResourceService>();
            }

            if (_coreManager != null)
            {
                builder.RegisterComponent(_coreManager);
            }

            if (_gridManager != null)
            {
                builder.RegisterComponent(_gridManager);
            }

            if (_buildingController != null)
            {
                builder.RegisterComponent(_buildingController);
            }

            if (_waveManager != null)
            {
                builder.RegisterComponent(_waveManager);
            }

            if (_hudView != null)
            {
                builder.RegisterComponent(_hudView);
            }

            builder.Register<TowerSelectionModel>(Lifetime.Singleton);
            builder.Register<TowerSelectionPresenter>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<TowerSelectionPresenter>().Initialize());

            if (_towerSelectionView != null)
            {
                builder.RegisterComponent(_towerSelectionView);
            }

            if (_towerCatalog != null)
            {
                builder.RegisterInstance(_towerCatalog);
            }

            builder.RegisterBuildCallback(resolver =>
            {
                foreach (HarvestableNode node in FindObjectsByType<HarvestableNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    resolver.InjectGameObject(node.gameObject);
                }
            });
        }
    }
}
