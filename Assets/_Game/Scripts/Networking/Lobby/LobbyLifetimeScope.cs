using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// VContainer scope cho Lobby scene. Pattern theo GameLifetimeScope.
    /// </summary>
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        [Header("Lobby Refs")]
        [SerializeField] private LobbyView _lobbyView;
        [SerializeField] private LobbyController _lobbyController;
        [SerializeField] private LobbyConnectionService _connectionService;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<LobbyModel>(Lifetime.Singleton);

            if (_lobbyView != null)
            {
                builder.RegisterComponent(_lobbyView);
            }

            if (_lobbyController != null)
            {
                builder.RegisterComponent(_lobbyController);
            }

            if (_connectionService != null)
            {
                builder.RegisterComponent(_connectionService);
            }

            builder.RegisterEntryPoint<LobbyPresenter>(Lifetime.Singleton).AsSelf();
        }
    }
}
