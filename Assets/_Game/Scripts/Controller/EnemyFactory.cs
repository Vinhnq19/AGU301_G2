using UnityEngine;

/// <summary>
/// Concrete Factory tạo các loại Enemy (Zombie, Skeleton, Boss) từ Prefab.
/// </summary>
public class EnemyFactory : Factory, IFactory
{
    [Header("Enemy Prefabs")]
    // Prefab của Zombie – kéo vào từ Project window
    [SerializeField] private GameObject zombiePrefab;

    // Prefab của Skeleton – kéo vào từ Project window
    [SerializeField] private GameObject skeletonPrefab;

    // Prefab của Boss – kéo vào từ Project window
    [SerializeField] private GameObject bossPrefab;

    /// <summary>
    /// Tạo enemy theo loại được yêu cầu và spawn tại vị trí chỉ định.
    /// Sau khi tạo, tự động gọi Initialize() trên enemy vừa spawn.
    /// </summary>
    /// <param name="type">Loại enemy: "Zombie", "Skeleton", hoặc "Boss"</param>
    /// <param name="position">Vị trí xuất hiện trong world space</param>
    /// <returns>GameObject enemy đã được tạo, hoặc null nếu loại không hợp lệ</returns>
    public override GameObject Create(string type, Vector3 position)
    {
        GameObject prefab = GetPrefabByType(type);

        if (prefab == null)
        {
            Debug.LogWarning($"[EnemyFactory] Loại enemy không hợp lệ hoặc Prefab chưa được gán: '{type}'");
            return null;
        }

        // Instantiate enemy tại vị trí yêu cầu
        GameObject enemyObj = Instantiate(prefab, position, Quaternion.identity);

        // Gọi Initialize nếu enemy implement IEnemy
        IEnemy enemy = enemyObj.GetComponent<IEnemy>();
        enemy?.Initialize();

        return enemyObj;
    }

    /// <summary>
    /// Lấy đúng Prefab tương ứng với loại enemy.
    /// </summary>
    /// <param name="type">Tên loại enemy</param>
    /// <returns>Prefab tương ứng, hoặc null nếu không tìm thấy</returns>
    private GameObject GetPrefabByType(string type)
    {
        return type switch
        {
            "Zombie"   => zombiePrefab,
            "Skeleton" => skeletonPrefab,
            "Boss"     => bossPrefab,
            _          => null
        };
    }
}
