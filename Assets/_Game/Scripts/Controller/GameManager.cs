// GameManager.cs
// Hệ thống quản lý trung tâm toàn cục của game, sử dụng Singleton Pattern.
// Quản lý điểm số, wave hiện tại và trạng thái game.

using System;
using UnityEngine;

/// <summary>
/// Singleton quản lý trạng thái toàn cục của game.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public int Score { get; private set; }
    public int CurrentWave { get; private set; }
    public bool IsGameRunning { get; private set; }

    /// <summary>
    /// Event được phát mỗi khi chuyển sang wave mới.
    /// EnemySpawner đăng ký sự kiện này để tự động spawn wave tiếp theo.
    /// </summary>
    public event Action OnNextWave;

    /// <summary>
    /// Khởi tạo GameManager và bắt đầu game ngay khi vào scene.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        StartGame();
    }

    /// <summary>
    /// Bắt đầu game mới: reset điểm, wave, và đặt trạng thái đang chạy.
    /// </summary>
    public void StartGame()
    {
        Score = 0;
        CurrentWave = 1;
        IsGameRunning = true;
        Debug.Log($"[GameManager] Game bắt đầu! Wave {CurrentWave}");
    }

    /// <summary>
    /// Kết thúc game và hiển thị điểm số cuối cùng.
    /// </summary>
    public void EndGame()
    {
        IsGameRunning = false;
        Debug.Log($"[GameManager] Game kết thúc! Điểm cuối: {Score}");
    }

    /// <summary>
    /// Cộng điểm cho người chơi khi tiêu diệt enemy.
    /// </summary>
    /// <param name="points">Số điểm cần cộng thêm</param>
    public void AddScore(int points)
    {
        Score += points;
        Debug.Log($"[GameManager] +{points} điểm! Tổng điểm: {Score}");
    }

    /// <summary>
    /// Chuyển sang wave tiếp theo.
    /// </summary>
    public void NextWave()
    {
        CurrentWave++;
        Debug.Log($"[GameManager] === WAVE {CurrentWave} BẮT ĐẦU! ===");

        // Thông báo cho tất cả subscriber (EnemySpawner) bắt đầu spawn wave mới
        OnNextWave?.Invoke();
    }
}
