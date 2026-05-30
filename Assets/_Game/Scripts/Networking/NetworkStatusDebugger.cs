using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Networking
{
    public sealed class NetworkStatusDebugger : MonoBehaviour
    {
        private NetworkManager _networkManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDebuggerExists()
        {
            if (FindFirstObjectByType<NetworkStatusDebugger>() != null || NetworkManager.Singleton == null)
            {
                return;
            }

            NetworkManager.Singleton.gameObject.AddComponent<NetworkStatusDebugger>();
        }

        private void Awake()
        {
            _networkManager = NetworkManager.Singleton;
        }

        private void OnEnable()
        {
            _networkManager ??= NetworkManager.Singleton;
            if (_networkManager == null)
            {
                DBLog.Warning("net.no-manager", "NetworkStatusDebugger enabled but NetworkManager.Singleton is null.", 2f, this);
                return;
            }

            _networkManager.OnServerStarted += HandleServerStarted;
            _networkManager.OnClientStarted += HandleClientStarted;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;

            DBLog.Info("net.debug-enabled", "Network debugger active.", 2f, this);
        }

        private void OnDisable()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnServerStarted -= HandleServerStarted;
            _networkManager.OnClientStarted -= HandleClientStarted;
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void Update()
        {
            if (_networkManager == null || !_networkManager.IsListening)
            {
                return;
            }

            DBLog.Info(
                "net.status",
                $"Network status: listening={_networkManager.IsListening}, host={_networkManager.IsHost}, server={_networkManager.IsServer}, client={_networkManager.IsClient}, localClientId={_networkManager.LocalClientId}, connected={_networkManager.ConnectedClientsIds.Count}.",
                5f,
                this);
        }

        private void HandleServerStarted()
        {
            DBLog.Info("net.server-started", "Server started.", 0f, this);
        }

        private void HandleClientStarted()
        {
            DBLog.Info("net.client-started", $"Client started. localClientId={_networkManager.LocalClientId}.", 0f, this);
        }

        private void HandleClientConnected(ulong clientId)
        {
            DBLog.Info($"net.client-connected.{clientId}", $"Client connected: {clientId}.", 0f, this);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            DBLog.Info($"net.client-disconnected.{clientId}", $"Client disconnected: {clientId}.", 0f, this);
        }
    }
}
