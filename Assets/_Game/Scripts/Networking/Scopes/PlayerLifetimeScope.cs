using DungeonBuilder.Player;
using DungeonBuilder.Player.Tools;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DungeonBuilder.Networking.Scopes
{
    public sealed class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeField] private InputReader _inputReader;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private ToolController _toolController;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_inputReader != null)
            {
                builder.RegisterComponent(_inputReader);
            }

            if (_playerController != null)
            {
                builder.RegisterComponent(_playerController);
            }

            if (_playerStats != null)
            {
                builder.RegisterComponent(_playerStats);
            }

            if (_toolController != null)
            {
                builder.RegisterComponent(_toolController);
            }
        }
    }
}
