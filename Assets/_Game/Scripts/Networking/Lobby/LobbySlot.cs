using System;
using Unity.Collections;
using Unity.Netcode;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// Mot slot trong hang cho lobby. Dong bo qua NetworkList.
    /// Pattern theo ResourceAmount (INetworkSerializable + IEquatable de dung trong NetworkList).
    /// </summary>
    public struct LobbySlot : INetworkSerializable, IEquatable<LobbySlot>
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName;

        public LobbySlot(ulong clientId, FixedString32Bytes playerName)
        {
            ClientId = clientId;
            PlayerName = playerName;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
        }

        public bool Equals(LobbySlot other)
        {
            return ClientId == other.ClientId && PlayerName.Equals(other.PlayerName);
        }

        public override bool Equals(object obj)
        {
            return obj is LobbySlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, PlayerName.GetHashCode());
        }
    }
}
