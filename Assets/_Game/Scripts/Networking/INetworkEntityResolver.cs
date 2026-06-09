using Unity.Netcode;

namespace DungeonBuilder.Networking
{
    public interface INetworkEntityResolver
    {
        bool TryGetPlayerObject(ulong clientId, out NetworkObject playerObject);
        bool TryGetSpawnedObject(ulong networkObjectId, out NetworkObject networkObject);
    }
}
