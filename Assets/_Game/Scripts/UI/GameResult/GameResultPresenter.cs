using DungeonBuilder.Core;
using DungeonBuilder.UI.Base;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace DungeonBuilder.UI.GameResult
{
    public sealed class GameResultPresenter : BasePresenter<GameResultView, GameResultModel>, IInitializable
    {
        private readonly EventBus _eventBus;

        public GameResultPresenter(GameResultView view, GameResultModel model, EventBus eventBus) : base(view, model)
        {
            _eventBus = eventBus;
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
            if (net == null)
            {
                SceneManager.LoadScene("LobbyScene");
                return;
            }

            if (net.IsHost)
            {
                net.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
            }
            else
            {
                net.Shutdown();
                SceneManager.LoadScene("LobbyScene");
            }
        }

        public bool IsVisible => Model.IsVisible;
        public bool IsWin => Model.IsWin;

        protected override void OnModelChanged()
        {
            View.Render();
        }

        private void HandleGameEnded(bool isWin)
        {
            Model.Show(isWin);
        }
    }
}
