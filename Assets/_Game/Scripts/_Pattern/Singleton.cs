using UnityEngine;

/// <summary>
/// Base class generic Singleton dành cho MonoBehaviour.
/// Kế thừa lớp này để biến bất kỳ MonoBehaviour nào thành Singleton.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // Instance duy nhất của kiểu T
    private static T _instance;

    /// <summary>
    /// Truy cập instance duy nhất của Singleton.
    /// Tự động tìm hoặc cảnh báo nếu chưa tồn tại.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<T>();

                if (_instance == null)
                    Debug.LogWarning($"[Singleton] Không tìm thấy instance của {typeof(T).Name} trong scene!");
            }
            return _instance;
        }
    }

    /// <summary>
    /// Khởi tạo Singleton: gán instance và đánh dấu DontDestroyOnLoad.
    /// Tự hủy nếu đã tồn tại một instance khác.
    /// </summary>
    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] Phát hiện instance trùng lặp của {typeof(T).Name}. Hủy object này.");
            Destroy(gameObject);
        }
    }
}
