using System;
using DungeonBuilder.Networking.Lobby;
using DungeonBuilder.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Chat
{
    /// <summary>
    /// In-scene NetworkBehaviour xu ly chat giua cac nguoi choi.
    /// Dat tren mot NetworkObject trong LobbyScene va SampleScene.
    /// - LobbyScene: keo LobbyController vao field _lobbyController de lookup ten.
    /// - SampleScene: de _lobbyController trong (null), se dung PlayerNameplate.
    /// </summary>
    public sealed class ChatManager : NetworkBehaviour
    {
        [SerializeField] private LobbyController _lobbyController;

        public event Action<string, string> OnMessageReceived;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SendChatMessageRpc(FixedString128Bytes message, RpcParams rpcParams = default)
        {
            string trimmed = message.ToString().Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            ulong senderId = rpcParams.Receive.SenderClientId;
            FixedString32Bytes senderName = ResolvePlayerName(senderId);
            ReceiveChatMessageRpc(senderName, new FixedString128Bytes(trimmed));
        }

        [Rpc(SendTo.Everyone)]
        private void ReceiveChatMessageRpc(FixedString32Bytes senderName, FixedString128Bytes message)
        {
            OnMessageReceived?.Invoke(senderName.ToString(), message.ToString());
        }

        private FixedString32Bytes ResolvePlayerName(ulong clientId)
        {
            if (_lobbyController != null)
            {
                foreach (LobbySlot slot in _lobbyController.Slots)
                {
                    if (slot.ClientId == clientId)
                    {
                        return slot.PlayerName;
                    }
                }
            }
            else if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient)
                     && networkClient.PlayerObject != null)
            {
                PlayerNameplate nameplate = networkClient.PlayerObject.GetComponentInChildren<PlayerNameplate>();
                if (nameplate != null)
                {
                    return nameplate.PlayerName.Value;
                }
            }

            return new FixedString32Bytes($"Player {clientId}");
        }
    }
}
