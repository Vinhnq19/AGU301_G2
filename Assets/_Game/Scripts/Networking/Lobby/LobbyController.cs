using System;
using System.Collections.Generic;
using DungeonBuilder.Core.Debugging;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// NetworkBehaviour giu danh sach nguoi choi trong hang cho (server-authoritative).
    /// Pattern NetworkList theo SharedResourceManager.
    /// Dat tren mot in-scene NetworkObject trong Lobby scene.
    /// </summary>
    public sealed class LobbyController : NetworkBehaviour
    {
        [Tooltip("Ten scene game se load khi host bam Start (phai co trong Build Settings).")]
        [SerializeField] private string _gameSceneName = "SampleScene";

        private readonly NetworkList<LobbySlot> _slots = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Ten nguoi choi cho moi clientId, set trong approval callback (chi ton tai tren server).</summary>
        private readonly Dictionary<ulong, string> _pendingNames = new();

        public event Action SlotsChanged;

        public IReadOnlyList<LobbySlot> Slots
        {
            get
            {
                var list = new List<LobbySlot>(_slots.Count);
                foreach (LobbySlot slot in _slots)
                {
                    list.Add(slot);
                }

                return list;
            }
        }

        public override void OnNetworkSpawn()
        {
            _slots.OnListChanged += HandleListChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;

                // Host khong di qua ConnectionApprovalCallback cho chinh minh,
                // nen lay ten host tu ConnectionData da set truoc StartHost().
                byte[] payload = NetworkManager.NetworkConfig.ConnectionData;
                if (payload != null && payload.Length > 0)
                {
                    _pendingNames[NetworkManager.LocalClientId] =
                        System.Text.Encoding.UTF8.GetString(payload);
                }

                // Host (server + client) cung phai co slot cua chinh minh.
                AddSlot(NetworkManager.LocalClientId);
            }

            SlotsChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _slots.OnListChanged -= HandleListChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        /// <summary>
        /// Goi tu LobbyConnectionService (tren server) khi approve mot client de luu ten truoc khi client connected.
        /// </summary>
        public void RegisterPendingName(ulong clientId, string playerName)
        {
            _pendingNames[clientId] = playerName;
        }

        /// <summary>Host bam Start: load game scene dong bo cho tat ca client.</summary>
        public void RequestStartGame()
        {
            if (!IsServer)
            {
                DBLog.Warning("lobby.start-not-server", "Chi host/server moi duoc start game.", 2f, this);
                return;
            }

            if (string.IsNullOrEmpty(_gameSceneName))
            {
                DBLog.Warning("lobby.no-scene", "Chua dat ten game scene tren LobbyController.", 0f, this);
                return;
            }

            DBLog.Info("lobby.start", $"Host start game -> load scene '{_gameSceneName}'.", 0f, this);

            // Spawn player cho moi client SAU khi ho load xong game scene.
            // (Khi connect o lobby ta dat CreatePlayerObject=false nen NGO khong tu spawn.)
            // GamePlayerSpawner song tren NetworkManager (DontDestroyOnLoad) nen ton tai qua scene,
            // khac voi LobbyController (in-scene, bi huy khi LobbyScene unload).
            
            Dictionary<ulong, string> names = new Dictionary<ulong, string>();
            foreach (var slot in _slots)
            {
                names[slot.ClientId] = slot.PlayerName.ToString();
            }

            GamePlayerSpawner.Arm(NetworkManager, _gameSceneName, names);

            NetworkManager.SceneManager.LoadScene(_gameSceneName, LoadSceneMode.Single);
        }

        private void HandleClientConnected(ulong clientId)
        {
            // Host da tu them slot cua minh trong OnNetworkSpawn.
            if (clientId == NetworkManager.LocalClientId)
            {
                return;
            }

            AddSlot(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            _pendingNames.Remove(clientId);

            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].ClientId == clientId)
                {
                    _slots.RemoveAt(i);
                }
            }
        }

        public void ChangeHostName(string newName)
        {
            if (!IsServer) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ClientId == NetworkManager.LocalClientId)
                {
                    _slots[i] = new LobbySlot(_slots[i].ClientId, Truncate(newName));
                    break;
                }
            }
        }

        private void AddSlot(ulong clientId)
        {
            // Khong them trung.
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ClientId == clientId)
                {
                    return;
                }
            }

            string name = _pendingNames.TryGetValue(clientId, out string n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : $"Player {clientId}";

            _slots.Add(new LobbySlot(clientId, Truncate(name)));
        }

        private static FixedString32Bytes Truncate(string value)
        {
            // FixedString32Bytes chua toi da ~29 ky tu UTF-8; cat de tranh overflow.
            const int max = 24;
            if (value.Length > max)
            {
                value = value.Substring(0, max);
            }

            return new FixedString32Bytes(value);
        }

        private void HandleListChanged(NetworkListEvent<LobbySlot> changeEvent)
        {
            SlotsChanged?.Invoke();
        }
    }
}
