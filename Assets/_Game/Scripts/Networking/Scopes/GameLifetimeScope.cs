using DungeonBuilder.Audio;
using DungeonBuilder.Building;
using DungeonBuilder.Chat;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using DungeonBuilder.Harvesting;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.UI.Chat;
using DungeonBuilder.UI.GameResult;
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
        [SerializeField] private GameAudioController _gameAudioController;
        [SerializeField] private ResourceSpawner _resourceSpawner;

        [Header("UI")]
        [SerializeField] private HUDView _hudView;
        [SerializeField] private TowerSelectionView _towerSelectionView;
        [SerializeField] private GameResultView _gameResultView;
        [SerializeField] private DungeonBuilder.UI.TowerAction.TowerActionPopupView _towerActionPopupView;
        [SerializeField] private Assets._Game.Scripts.UI.WaveAnnouncementView _waveAnnouncementView;
        [SerializeField] private Assets._Game.Scripts.UI.Tutorial.TutorialManager _tutorialManager;

        [Header("Chat")]
        [SerializeField] private ChatManager _chatManager;
        [SerializeField] private ChatView _chatView;

        [Header("Data")]
        [SerializeField] private TowerCatalogSO _towerCatalog;
        [SerializeField] private TowerUnlockConfigSO _towerUnlockConfig;

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

                // Nguồn wave data: Editor/Dev build cho phép override bằng StreamingAssets/waves.json
                // (hot reload qua Cheat Panel); build release luôn đọc WaveCatalog SO.
                builder.Register<IWaveProvider>(_ =>
                {
                    IWaveProvider soProvider = new SoWaveProvider(_waveManager.WaveCatalog);
                    if (Application.isEditor || Debug.isDebugBuild)
                    {
                        return new JsonWaveProvider(soProvider);
                    }
                    return soProvider;
                }, Lifetime.Singleton);
            }

            if (AudioManager.Instance != null)
            {
                builder.RegisterInstance<IAudioService>(AudioManager.Instance);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] AudioManager.Instance is null. Is AudioManager prefab in the boot scene?");
            }

            if (_gameAudioController != null)
            {
                builder.RegisterComponent(_gameAudioController);
            }

            if (_resourceSpawner != null)
            {
                builder.RegisterComponent(_resourceSpawner);
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

            if (_waveAnnouncementView != null)
            {
                builder.RegisterComponent(_waveAnnouncementView);
            }

            if (_tutorialManager != null)
            {
                builder.RegisterComponent(_tutorialManager);
            }

            if (_towerActionPopupView != null)
            {
                builder.RegisterComponent(_towerActionPopupView);
                builder.Register<DungeonBuilder.UI.TowerAction.TowerActionPopupPresenter>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            }

            if (_towerCatalog != null)
            {
                builder.RegisterInstance(_towerCatalog);
            }

            // Always register a TowerUnlockConfigSO so injection into TowerSelectionPresenter /
            // BuildingController never fails. Fall back to a default instance (Arrow unlocked) if unassigned.
            builder.RegisterInstance(_towerUnlockConfig != null ? _towerUnlockConfig : ScriptableObject.CreateInstance<TowerUnlockConfigSO>());

            builder.Register<GameStatsTracker>(Lifetime.Singleton);
            builder.Register<GameResultModel>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameResultPresenter>(Lifetime.Singleton);
            if (_gameResultView != null)
                builder.RegisterComponent(_gameResultView);

            if (_chatManager != null)
            {
                builder.RegisterComponent(_chatManager);
            }

            if (_chatView != null)
            {
                builder.RegisterComponent(_chatView);
            }

            builder.Register<ChatModel>(Lifetime.Singleton);
            builder.Register<ChatPresenter>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<ChatPresenter>().Initialize());

            builder.RegisterBuildCallback(resolver =>
            {
                if (_gameAudioController != null)
                {
                    resolver.Inject(_gameAudioController);
                }

                foreach (HarvestableNode node in FindObjectsByType<HarvestableNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    resolver.InjectGameObject(node.gameObject);
                }

                // Inject MOI Shop trong scene (shop tai nguyen, shop upgrade, ...).
                // Truoc day dua vao autoInjectGameObjects — de sot shop moi them vao scene
                // (bug thuc te: "Shop - Upgrade" khong duoc inject -> _sharedResources null
                // -> moi giao dich fail im lang). Construct da idempotent nen inject trung
                // voi autoInject cung khong sao.
                foreach (Shop shop in FindObjectsByType<Shop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    resolver.Inject(shop);
                }
            });
        }
    }
}
