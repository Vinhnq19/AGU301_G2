using Unity.Netcode;
using UnityEngine;

public class NetworkEnemyFactory : EnemyFactory
{
    /// <summary>
    /// Spawn enemy qua mạng. Chỉ gọi từ Server.
    /// Sau khi NetworkObject.Spawn(), tất cả client đều thấy enemy.
    /// </summary>
    public GameObject NetworkCreate(string type, Vector3 position)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkEnemyFactory] Chỉ Server mới được spawn enemy!");
            RequestSpawnEnemyServerRpc(type, position);
            return null;
        }
        // Dùng logic Create() từ EnemyFactory cha
        GameObject enemyObj = Create(type, position);
        if (enemyObj != null)
        {
            NetworkObject netObj = enemyObj.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn(true); // true = destroy when server disconnects
            else
                Debug.LogError("[NetworkEnemyFactory] Enemy Prefab thiếu NetworkObject component!");
        }
        return enemyObj;
    }
    /// <summary>
    /// Client gửi request lên Server để spawn enemy.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnEnemyServerRpc(string type, Vector3 position)
    {
        NetworkCreate(type, position);
    }

}