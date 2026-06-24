using DungeonBuilder.UI.Base;
using Unity.Netcode;
using VContainer.Unity;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// Presenter cho man hinh lobby (hang cho). Noi UI <-> LobbyConnectionService + LobbyController.
    /// </summary>
    public sealed class LobbyPresenter : BasePresenter<LobbyView, LobbyModel>, IInitializable
    {
        private readonly LobbyConnectionService _connection;
        private readonly LobbyController _controller;

        public LobbyPresenter(
            LobbyView view,
            LobbyModel model,
            LobbyConnectionService connection,
            LobbyController controller)
            : base(view, model)
        {
            _connection = connection;
            _controller = controller;
        }

        /// <summary>Cho View doc state de render (Model la protected trong BasePresenter).</summary>
        public LobbyModel ModelForView => Model;

        public override void Initialize()
        {
            View.SetPresenter(this);

            Model.SetLocalIp(_connection.GetLocalIPv4());

            _connection.StatusChanged += HandleStatusChanged;

            if (_controller != null)
            {
                _controller.SlotsChanged += HandleSlotsChanged;
            }

            base.Initialize();
        }

        /// <summary>Nguoi choi bam Host.</summary>
        public void OnHostClicked(string playerName)
        {
            Model.SetState(LobbyConnectionState.Connecting);
            if (_connection.StartHost(playerName))
            {
                Model.SetIsHost(true);
                Model.SetState(LobbyConnectionState.InLobby);
            }
            else
            {
                Model.SetState(LobbyConnectionState.Disconnected);
            }
        }

        public void OnPlayerNameChanged(string newName)
        {
            if (Model.IsHost && _controller != null)
            {
                _controller.ChangeHostName(newName);
            }
        }

        /// <summary>Nguoi choi bam Join voi IP (ID phong).</summary>
        public void OnJoinClicked(string hostIp, string playerName)
        {
            Model.SetState(LobbyConnectionState.Connecting);
            if (_connection.StartClient(hostIp, playerName))
            {
                Model.SetIsHost(false);
                Model.SetState(LobbyConnectionState.InLobby);
            }
            else
            {
                Model.SetState(LobbyConnectionState.Disconnected);
            }
        }

        /// <summary>Host bam Start -> load game scene cho tat ca.</summary>
        public void OnStartClicked()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _controller?.RequestStartGame();
        }

        public void OnDisconnectClicked()
        {
            _connection.Disconnect();
            Model.SetIsHost(false);
            Model.SetState(LobbyConnectionState.Disconnected);
            Model.SetSlots(System.Array.Empty<LobbySlot>());
        }

        protected override void OnModelChanged()
        {
            View.Render();
        }

        public override void Dispose()
        {
            _connection.StatusChanged -= HandleStatusChanged;
            if (_controller != null)
            {
                _controller.SlotsChanged -= HandleSlotsChanged;
            }

            base.Dispose();
        }

        private void HandleStatusChanged(string status)
        {
            Model.SetStatus(status);
        }

        private void HandleSlotsChanged()
        {
            Model.SetSlots(_controller.Slots);
        }
    }
}
