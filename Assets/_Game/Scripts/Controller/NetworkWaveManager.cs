// Assets/_Game/Scripts/Controller/NetworkWaveManager.cs
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Quản lý wave enemy qua mạng. Chỉ chạy trên Server.
/// </summary>
public class NetworkWaveManager : NetworkBehaviour
{
    [SerializeField] private NetworkEnemyFactory enemyFactory;
    [SerializeField] private Transform[] spawnPoints;

    private NetworkVariable<int> _currentWave = new NetworkVariable<int>(0);
    public int CurrentWave => _currentWave.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(StartWaveLoop());
    }

    private IEnumerator StartWaveLoop()
    {
        yield return new WaitForSeconds(3f); // Delay trước khi bắt đầu

        while (true)
        {
            _currentWave.Value++;
            SpawnWaveClientRpc(_currentWave.Value);
            yield return SpawnEnemiesForWave(_currentWave.Value);
            yield return new WaitForSeconds(10f); // Nghỉ giữa wave
        }
    }

    private IEnumerator SpawnEnemiesForWave(int wave)
    {
        int enemyCount = 3 + wave * 2; // Wave càng cao càng nhiều enemy

        for (int i = 0; i < enemyCount; i++)
        {
            string type = wave >= 5 ? "Boss" : (i % 2 == 0 ? "Zombie" : "Skeleton");
            Vector3 pos = spawnPoints[i % spawnPoints.Length].position;

            enemyFactory.NetworkCreate(type, pos);
            yield return new WaitForSeconds(0.5f);
        }
    }

    [ClientRpc]
    private void SpawnWaveClientRpc(int wave)
    {
        Debug.Log($"[Wave] ⚔️ Wave {wave} bắt đầu!");
        // Hiện UI thông báo wave
    }
}
