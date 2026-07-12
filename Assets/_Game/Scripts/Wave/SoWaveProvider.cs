using DungeonBuilder.Data;

namespace DungeonBuilder.Wave
{
    /// <summary>Provider mặc định: đọc thẳng từ WaveCatalogSO (asset sinh bởi WaveSheetImporter).</summary>
    public sealed class SoWaveProvider : IWaveProvider
    {
        private readonly WaveCatalogSO _catalog;

        public SoWaveProvider(WaveCatalogSO catalog)
        {
            _catalog = catalog;
        }

        public int WaveCount => _catalog != null && _catalog.waves != null ? _catalog.waves.Count : 0;

        public WaveData GetWave(int index)
        {
            WaveSO wave = _catalog.waves[index];
            return new WaveData
            {
                buildPhaseDuration = wave.buildPhaseDuration,
                combatPhaseDuration = wave.combatPhaseDuration,
                isBossWave = wave.isBossWave,
                spawnGroups = wave.spawnGroups
            };
        }

        public void Reload()
        {
            // SO là nguồn tĩnh — không có gì để reload.
        }
    }
}
