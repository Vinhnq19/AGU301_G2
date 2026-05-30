using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Networking.Pool
{
    public interface INetworkPool
    {
        NetworkObject Get(NetworkObject prefab, Vector3 position, Quaternion rotation);

        void Return(NetworkObject networkObject);
    }
}
