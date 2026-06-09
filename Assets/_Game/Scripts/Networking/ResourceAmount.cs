using System;
using DungeonBuilder.Core.Enums;
using Unity.Netcode;

namespace DungeonBuilder.Networking
{
    public struct ResourceAmount : INetworkSerializable, IEquatable<ResourceAmount>
    {
        public ResourceType Type;
        public int Amount;

        public ResourceAmount(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Amount);
        }

        public bool Equals(ResourceAmount other)
        {
            return Type == other.Type && Amount == other.Amount;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceAmount other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Type, Amount);
        }
    }
}
