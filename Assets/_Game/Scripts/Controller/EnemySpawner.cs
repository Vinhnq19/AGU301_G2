using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyFactory enemyFactory;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnDelay = 1.5f;
    [SerializeField] private float waveStartDelay = 2f;

    // Tập hợp các key enemy — phải khớp với PoolEntry.key trong ObjectPool Inspector
    private static readonly string[] EnemyKeys = { "Zombie", "Skeleton", "Boss" };

    private void Start()
    {
        GameManager.Instance.OnNextWave += OnWaveStarted;
        StartCoroutine(SpawnWave());
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnNextWave -= OnWaveStarted;
    }

    private void OnWaveStarted()
    {
        StartCoroutine(SpawnWaveWithDelay());
    }

    private IEnumerator SpawnWaveWithDelay()
    {
        Debug.Log($"[EnemySpawner] Wave {GameManager.Instance.CurrentWave} sẽ bắt đầu sau {waveStartDelay}s...");
        yield return new WaitForSeconds(waveStartDelay);
        StartCoroutine(SpawnWave());
    }

    private IEnumerator SpawnWave()
    {
        List<string> enemyList = BuildWaveEnemyList(GameManager.Instance.CurrentWave);

        Debug.Log($"[EnemySpawner] Bắt đầu spawn wave {GameManager.Instance.CurrentWave} với {enemyList.Count} enemy.");

        foreach (string enemyType in enemyList)
        {
            if (!GameManager.Instance.IsGameRunning)
                yield break;

            Vector3 spawnPosition = GetRandomSpawnPoint();

            // ── Ưu tiên lấy từ ObjectPool ──────────────────────────────
            // Nếu chưa có ObjectPool trong scene, fallback sang Factory
            if (ObjectPool.Instance != null)
            {
                GameObject enemyObj = ObjectPool.Instance.Get(enemyType, spawnPosition);

                // Gọi Initialize nếu enemy implement IEnemy
                if (enemyObj != null)
                    enemyObj.GetComponent<IEnemy>()?.Initialize();
            }
            else
            {
                // Fallback: dùng Factory như cũ (đảm bảo backward-compatible)
                enemyFactory.Create(enemyType, spawnPosition);
            }

            yield return new WaitForSeconds(spawnDelay);
        }

        Debug.Log("[EnemySpawner] Đã spawn xong tất cả enemy trong wave. Tiêu diệt Boss để qua wave tiếp!");
    }

    private List<string> BuildWaveEnemyList(int wave)
    {
        List<string> list = new List<string>();

        // Zombie: base 2, tăng 1 mỗi wave
        int zombieCount = 1 + wave;
        for (int i = 0; i < zombieCount; i++)
            list.Add("Zombie");

        // Skeleton: bắt đầu từ wave 2, tăng 1 mỗi 2 wave
        int skeletonCount = Mathf.Max(0, wave - 1);
        for (int i = 0; i < skeletonCount; i++)
            list.Add("Skeleton");

        // Luôn có 1 Boss cuối mỗi wave
        list.Add("Boss");

        return list;
    }

    private Vector3 GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] Chưa gán SpawnPoints! Spawn tại vị trí gốc.");
            return Vector3.zero;
        }

        int randomIndex = Random.Range(0, spawnPoints.Length);
        return spawnPoints[randomIndex].position;
    }

    // ── Helper: trả enemy về pool khi nó chết ─────────────────────────────
    // Gọi từ script Enemy khi HP = 0 (thay vì Destroy)
    public static void ReturnEnemyToPool(string enemyType, GameObject enemyObj)
    {
        if (ObjectPool.Instance != null)
            ObjectPool.Instance.Return(enemyType, enemyObj);
        else
            Destroy(enemyObj);
    }
}
