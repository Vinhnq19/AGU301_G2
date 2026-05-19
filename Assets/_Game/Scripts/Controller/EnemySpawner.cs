// EnemySpawner.cs
// Điều phối việc spawn hàng loạt enemy qua EnemyFactory theo từng wave.
// Tự động spawn wave tiếp theo khi nhận event OnNextWave từ GameManager.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều phối spawn enemy theo wave sử dụng EnemyFactory.
/// Lắng nghe event OnNextWave từ GameManager để tự động spawn wave tiếp theo.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    // Reference đến EnemyFactory để tạo enemy
    [SerializeField] private EnemyFactory enemyFactory;

    [Header("Spawn Settings")]
    // Danh sách vị trí có thể spawn enemy trong scene
    [SerializeField] private Transform[] spawnPoints;

    // Thời gian delay (giây) giữa mỗi lần spawn trong wave
    [SerializeField] private float spawnDelay = 1.5f;

    // Thời gian chờ (giây) trước khi bắt đầu wave mới sau khi Boss chết
    [SerializeField] private float waveStartDelay = 2f;

    /// <summary>
    /// Đăng ký lắng nghe event OnNextWave và bắt đầu wave 1.
    /// </summary>
    private void Start()
    {
        // Đăng ký callback để tự động spawn khi GameManager phát event NextWave
        GameManager.Instance.OnNextWave += OnWaveStarted;

        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Hủy đăng ký event khi object bị destroy để tránh memory leak.
    /// </summary>
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnNextWave -= OnWaveStarted;
    }

    /// <summary>
    /// Callback được gọi tự động khi GameManager.NextWave() phát event.
    /// Chờ một khoảng rồi spawn wave mới.
    /// </summary>
    private void OnWaveStarted()
    {
        StartCoroutine(SpawnWaveWithDelay());
    }

    /// <summary>
    /// Chờ waveStartDelay giây rồi bắt đầu spawn wave tiếp theo.
    /// </summary>
    private IEnumerator SpawnWaveWithDelay()
    {
        Debug.Log($"[EnemySpawner] Wave {GameManager.Instance.CurrentWave} sẽ bắt đầu sau {waveStartDelay}s...");
        yield return new WaitForSeconds(waveStartDelay);
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Coroutine spawn từng enemy trong wave với delay giữa mỗi lần.
    /// Số lượng enemy tăng dần theo wave hiện tại.
    /// </summary>
    private IEnumerator SpawnWave()
    {
        List<string> enemyList = BuildWaveEnemyList(GameManager.Instance.CurrentWave);

        Debug.Log($"[EnemySpawner] Bắt đầu spawn wave {GameManager.Instance.CurrentWave} với {enemyList.Count} enemy.");

        foreach (string enemyType in enemyList)
        {
            // Dừng nếu game không còn chạy
            if (!GameManager.Instance.IsGameRunning)
                yield break;

            // Chọn điểm spawn ngẫu nhiên
            Vector3 spawnPosition = GetRandomSpawnPoint();

            // Dùng Factory tạo enemy
            enemyFactory.Create(enemyType, spawnPosition);

            yield return new WaitForSeconds(spawnDelay);
        }

        Debug.Log("[EnemySpawner] Đã spawn xong tất cả enemy trong wave. Tiêu diệt Boss để qua wave tiếp!");
    }

    /// <summary>
    /// Xây dựng danh sách enemy cho wave dựa trên số wave hiện tại.
    /// Wave càng cao càng có nhiều enemy và loại khó hơn.
    /// </summary>
    /// <param name="wave">Số wave hiện tại</param>
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

    /// <summary>
    /// Trả về vị trí spawn ngẫu nhiên từ danh sách SpawnPoints.
    /// Nếu không có SpawnPoint nào, trả về vị trí gốc (0,0,0).
    /// </summary>
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
}
