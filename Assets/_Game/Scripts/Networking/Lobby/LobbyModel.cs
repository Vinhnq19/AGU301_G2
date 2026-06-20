using System;
using System.Collections.Generic;
using DungeonBuilder.UI.Base;

namespace DungeonBuilder.Networking.Lobby
{
    public enum LobbyConnectionState
    {
        Disconnected,
        Connecting,
        InLobby
    }

    public sealed class LobbyModel : IModel
    {
        private readonly List<LobbySlot> _slots = new();

        public event Action OnChanged;

        public IReadOnlyList<LobbySlot> Slots => _slots;
        public string LocalIp { get; private set; } = "127.0.0.1";
        public bool IsHost { get; private set; }
        public LobbyConnectionState State { get; private set; } = LobbyConnectionState.Disconnected;
        public string Status { get; private set; } = string.Empty;

        public void SetSlots(IReadOnlyList<LobbySlot> slots)
        {
            _slots.Clear();
            if (slots != null)
            {
                _slots.AddRange(slots);
            }

            OnChanged?.Invoke();
        }

        public void SetLocalIp(string ip)
        {
            LocalIp = ip;
            OnChanged?.Invoke();
        }

        public void SetIsHost(bool isHost)
        {
            IsHost = isHost;
            OnChanged?.Invoke();
        }

        public void SetState(LobbyConnectionState state)
        {
            State = state;
            OnChanged?.Invoke();
        }

        public void SetStatus(string status)
        {
            Status = status ?? string.Empty;
            OnChanged?.Invoke();
        }
    }
}
