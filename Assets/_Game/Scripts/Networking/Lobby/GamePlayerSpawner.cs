using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// Spawn player object cho moi client khi ho load xong game scene.
    /// Vi lobby connect voi CreatePlayerObject=false (khong spawn o lobby), nen phai spawn thu cong khi vao game.
    ///
    /// Component nay duoc gan dong len NetworkManager (DontDestroyOnLoad) qua Arm(), nen ton tai
    /// qua qua trinh load scene Single (LobbyController bi huy khi LobbyScene unload, con cai nay thi khong).
    /// </summary>
    public sealed class GamePlayerSpawner : MonoBehaviour
    {
        private NetworkManager _net;
        private string _gameSceneName;
        private bool _armed;

        /// <summary>Goi tren server truoc khi LoadScene. Idempotent.</summary>
        public static void Arm(NetworkManager net, string gameSceneName)
        {
            if (net == null)
            {
                return;
            }

            GamePlayerSpawner spawner = net.GetComponent<GamePlayerSpawner>();
            if (spawner == null)
            {
                spawner = net.gameObject.AddComponent<GamePlayerSpawner>();
            }

            spawner._net = net;
            spawner._gameSceneName = gameSceneName;

            // Unsubscribe trước để tránh duplicate nếu Arm() được gọi lại (ví dụ: sau Shutdown → StartHost lại).
            // SceneManager bị reset sau Shutdown nên unsubscribe cũ là no-op, sau đó subscribe mới là bắt buộc.
            if (spawner._armed)
                net.SceneManager.OnLoadComplete -= spawner.HandleLoadComplete;

            net.SceneManager.OnLoadComplete += spawner.HandleLoadComplete;
            spawner._armed = true;
        }

        private void OnDestroy()
        {
            if (_armed && _net != null && _net.SceneManager != null)
            {
                _net.SceneManager.OnLoadComplete -= HandleLoadComplete;
            }
        }

        private void HandleLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
        {
            if (_net == null || !_net.IsServer || sceneName != _gameSceneName)
            {
                return;
            }

            SpawnPlayerFor(clientId);
        }

        private void SpawnPlayerFor(ulong clientId)
        {
            // Tranh spawn trung neu da co player object.
            if (_net.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                && client.PlayerObject != null)
            {
                return;
            }

            GameObject playerPrefab = _net.NetworkConfig.PlayerPrefab;
            if (playerPrefab == null)
            {
                DBLog.Warning("spawn.no-player-prefab", "NetworkConfig.PlayerPrefab null, khong spawn duoc player.", 0f, this);
                return;
            }

            NetworkObject prefabNo = playerPrefab.GetComponent<NetworkObject>();
            if (prefabNo == null)
            {
                DBLog.Warning("spawn.player-no-netobj", "PlayerPrefab thieu NetworkObject.", 0f, this);
                return;
            }

            _net.SpawnManager.InstantiateAndSpawn(
                prefabNo,
                ownerClientId: clientId,
                destroyWithScene: true,
                isPlayerObject: true);

            DBLog.Info($"spawn.player.{clientId}", $"Spawned player cho client {clientId}.", 0f, this);
        }
    }
}
