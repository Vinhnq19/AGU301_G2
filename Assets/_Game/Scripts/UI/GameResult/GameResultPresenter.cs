using System.Collections.Generic;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace DungeonBuilder.UI.GameResult
{
    public sealed class GameResultPresenter : BasePresenter<GameResultView, GameResultModel>, IInitializable
    {
        private readonly EventBus _eventBus;
        private readonly GameStatsTracker _statsTracker;

        public GameResultPresenter(GameResultView view, GameResultModel model, EventBus eventBus, GameStatsTracker statsTracker)
            : base(view, model)
        {
            _eventBus = eventBus;
            _statsTracker = statsTracker;
            _eventBus.OnGameEnded += HandleGameEnded;
        }

        public override void Initialize()
        {
            View.SetPresenter(this);
            base.Initialize();
        }

        public override void Dispose()
        {
            _eventBus.OnGameEnded -= HandleGameEnded;
            base.Dispose();
        }

        public void ReturnToLobby()
        {
            var net = NetworkManager.Singleton;
            if (net != null && net.IsListening)
                net.Shutdown();

            SceneManager.LoadScene("LobbyScene");
        }

        public bool IsVisible      => Model.IsVisible;
        public bool IsWin          => Model.IsWin;
        public int EnemyKillCount  => Model.EnemyKillCount;
        public int BossKillCount   => Model.BossKillCount;
        public int WavesCompleted  => Model.WavesCompleted;
        public int TowersBuilt     => Model.TowersBuilt;
        public IReadOnlyDictionary<ResourceType, int> FinalResources => Model.FinalResources;

        protected override void OnModelChanged()
        {
            View.Render();
        }

        private void HandleGameEnded(bool isWin)
        {
            Model.Show(isWin, _statsTracker);
        }
    }
}
