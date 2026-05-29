using UnityEngine;
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
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
