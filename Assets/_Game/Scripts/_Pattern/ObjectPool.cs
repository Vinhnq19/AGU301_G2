using System.Collections.Generic;
using UnityEngine;

///   1. Đặt script này lên một GameObject trống (ví dụ: "ObjectPoolManager").
///   2. Khai báo danh sách PoolEntry trong Inspector (key + prefab + initialSize).
///   3. Gọi ObjectPool.Instance.Get("key", pos) để lấy, .Return(key, obj) để trả về.
/// </summary>
public class ObjectPool : Singleton<ObjectPool>
{
    [Header("Pool Entries")]
    [SerializeField] private List<PoolEntry> entries = new List<PoolEntry>();

    [Tooltip("Nếu pool hết, tự động tạo thêm thay vì trả về null")]
    [SerializeField] private bool expandable = true;

    private Dictionary<string, Queue<GameObject>> _available
        = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, List<GameObject>> _all
        = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, GameObject> _prefabs
        = new Dictionary<string, GameObject>();

    // ══════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        foreach (PoolEntry entry in entries)
        {
            if (string.IsNullOrEmpty(entry.key) || entry.prefab == null)
            {
                Debug.LogWarning("[ObjectPool] PoolEntry thiếu key hoặc prefab — bỏ qua.");
                continue;
            }

            _available[entry.key] = new Queue<GameObject>();
            _all[entry.key] = new List<GameObject>();
            _prefabs[entry.key] = entry.prefab;

            Prewarm(entry.key, entry.initialSize);
        }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>
    /// Lấy một GameObject theo key từ pool.
    /// </summary>
    /// <param name="key">Key đã khai báo trong Inspector (ví dụ: "Zombie")</param>
    /// <param name="position">Vị trí spawn trong world space</param>
    /// <param name="rotation">Rotation khi spawn</param>
    public GameObject Get(string key, Vector3 position, Quaternion rotation = default)
    {
        if (!_available.ContainsKey(key))
        {
            Debug.LogError($"[ObjectPool] Key '{key}' chưa được đăng ký trong Inspector!");
            return null;
        }

        GameObject obj;

        if (_available[key].Count > 0)
        {
            obj = _available[key].Dequeue();
        }
        else if (expandable)
        {
            obj = CreateObject(key);
            Debug.LogWarning($"[ObjectPool] Pool '{key}' đã hết — tạo thêm object.");
        }
        else
        {
            Debug.LogError($"[ObjectPool] Pool '{key}' đã hết và không thể mở rộng!");
            return null;
        }

        obj.transform.SetPositionAndRotation(
            position,
            rotation == default ? Quaternion.identity : rotation
        );
        obj.SetActive(true);
        obj.GetComponent<IPoolable>()?.OnGetFromPool();

        return obj;
    }

    /// <summary>
    /// Trả object về pool theo key. Gọi thay vì Destroy().
    /// </summary>
    /// <param name="key">Key của loại object này</param>
    /// <param name="obj">Object cần trả về</param>
    public void Return(string key, GameObject obj)
    {
        if (obj == null) return;

        if (!_available.ContainsKey(key))
        {
            Debug.LogWarning($"[ObjectPool] Return: Key '{key}' không tồn tại — Destroy thay thế.");
            Destroy(obj);
            return;
        }

        obj.GetComponent<IPoolable>()?.OnReturnToPool();
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        _available[key].Enqueue(obj);
    }

    /// <summary>
    /// Trả tất cả object đang active của một key về pool.
    /// </summary>
    public void ReturnAll(string key)
    {
        if (!_all.ContainsKey(key)) return;

        foreach (GameObject obj in _all[key])
        {
            if (obj != null && obj.activeSelf)
                Return(key, obj);
        }
    }

    /// <summary>
    /// Trả tất cả object của tất cả key về pool (ví dụ: restart level).
    /// </summary>
    public void ReturnAll()
    {
        foreach (string key in _all.Keys)
            ReturnAll(key);
    }

    /// <summary>Số object rảnh theo key.</summary>
    public int AvailableCount(string key)
        => _available.ContainsKey(key) ? _available[key].Count : 0;

    /// <summary>Tổng object đã tạo theo key.</summary>
    public int TotalCount(string key)
        => _all.ContainsKey(key) ? _all[key].Count : 0;

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Private Helpers

    private void Prewarm(string key, int count)
    {
        for (int i = 0; i < count; i++)
            _available[key].Enqueue(CreateObject(key));

        Debug.Log($"[ObjectPool] Prewarm '{key}' × {count} object.");
    }

    private GameObject CreateObject(string key)
    {
        GameObject obj = Instantiate(_prefabs[key], transform);
        obj.SetActive(false);
        _all[key].Add(obj);
        return obj;
    }

    #endregion
}

// ══════════════════════════════════════════════════════════════════════════
/// <summary>
/// Khai báo một loại pool trong Inspector.
/// </summary>
[System.Serializable]
public class PoolEntry
{
    [Tooltip("Key dùng để Get/Return (ví dụ: 'Zombie', 'Skeleton', 'Boss')")]
    public string key;

    [Tooltip("Prefab tương ứng với key")]
    public GameObject prefab;

    [Tooltip("Số object tạo sẵn khi game bắt đầu")]
    public int initialSize = 5;
}

// ══════════════════════════════════════════════════════════════════════════
/// <summary>
/// Interface tuỳ chọn — implement trên script của pooled object để nhận callback
/// khi object được lấy ra hoặc trả về pool.
/// Ví dụ: Reset trail renderer, dừng animation, clear bullet logic...
/// </summary>
public interface IPoolable
{
    /// <summary>Gọi ngay sau khi object được lấy ra khỏi pool (SetActive(true)).</summary>
    void OnGetFromPool();

    /// <summary>Gọi ngay trước khi object được trả về pool (SetActive(false)).</summary>
    void OnReturnToPool();
}
