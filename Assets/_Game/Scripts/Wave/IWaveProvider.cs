using System.Collections.Generic;
using DungeonBuilder.Data;

namespace DungeonBuilder.Wave
{
    /// <summary>
    /// DTO thuần mirror WaveSO để nguồn dữ liệu wave không phụ thuộc ScriptableObject
    /// (SoWaveProvider trả thẳng data từ SO, JsonWaveProvider dựng từ waves.json).
    /// </summary>
    public struct WaveData
    {
        public float buildPhaseDuration;
        public float combatPhaseDuration;
        public bool isBossWave;
        public IReadOnlyList<SpawnGroup> spawnGroups;
    }

    /// <summary>
    /// Nguồn wave data cho WaveManager. Đăng ký qua VContainer trong GameLifetimeScope:
    /// SoWaveProvider (mặc định, build release) hoặc JsonWaveProvider (Editor/Dev build,
    /// override bằng StreamingAssets/waves.json). Xem Docs/WAVE_DATA_PIPELINE_PLAN.md.
    /// </summary>
    public interface IWaveProvider
    {
        int WaveCount { get; }

        /// <summary>Lấy wave theo index 0-based. Caller tự clamp index vào [0, WaveCount).</summary>
        WaveData GetWave(int index);

        /// <summary>Đọc lại nguồn dữ liệu (hot reload). No-op với nguồn tĩnh như SO.</summary>
        void Reload();
    }
}
