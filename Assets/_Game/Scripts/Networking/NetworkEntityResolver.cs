using Unity.Netcode;

namespace DungeonBuilder.Networking
{
    public sealed class NetworkEntityResolver : INetworkEntityResolver
    {
        public bool TryGetPlayerObject(ulong clientId, out NetworkObject playerObject)
        {
            playerObject = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null
                || !manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return false;
            }

            playerObject = client.PlayerObject;
            return true;
        }

        public bool TryGetSpawnedObject(ulong networkObjectId, out NetworkObject networkObject)
        {
            networkObject = null;
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null
                && manager.SpawnManager != null
                && manager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out networkObject)
                && networkObject != null;
        }
    }
}
