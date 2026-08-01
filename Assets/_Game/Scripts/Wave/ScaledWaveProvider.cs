using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Data;
using UnityEngine;

namespace DungeonBuilder.Wave
{
    /// <summary>
    /// Bọc ngoài một <see cref="IWaveProvider"/> và áp dụng độ khó theo SỐ NGƯỜI CHƠI.
    ///
    /// - Nếu bậc độ khó có <c>overrideCatalog</c> → đọc hẳn bộ level riêng đó.
    /// - Sau đó nhân số quái / nhịp spawn / thời gian build theo hệ số của bậc.
    ///
    /// Số người chơi được lấy TRỄ (lần đầu cần tới dữ liệu wave) chứ không phải lúc dựng
    /// container: lúc scene game vừa load, client có thể chưa kết nối xong.
    /// </summary>
    public sealed class ScaledWaveProvider : IWaveProvider
    {
        private readonly IWaveProvider _baseProvider;
        private readonly DifficultyConfigSO _config;
        private readonly Func<int> _playerCountProvider;

        private IWaveProvider _activeProvider;
        private DifficultyConfigSO.Tier _tier;
        private bool _resolved;

        public ScaledWaveProvider(IWaveProvider baseProvider, DifficultyConfigSO config, Func<int> playerCountProvider)
        {
            _baseProvider = baseProvider;
            _config = config;
            _playerCountProvider = playerCountProvider;
        }

        /// <summary>Hệ số máu quái của bậc đang áp dụng — WaveManager đọc khi spawn.</summary>
        public float EnemyHealthMultiplier
        {
            get
            {
                EnsureResolved();
                return _tier.enemyHealthMultiplier;
            }
        }

        /// <summary>Nhãn bậc đang áp dụng (để log/hiển thị).</summary>
        public string TierLabel
        {
            get
            {
                EnsureResolved();
                return string.IsNullOrEmpty(_tier.label) ? "Default" : _tier.label;
            }
        }

        public int WaveCount
        {
            get
            {
                EnsureResolved();
                return _activeProvider.WaveCount;
            }
        }

        public WaveData GetWave(int index)
        {
            EnsureResolved();
            return Scale(_activeProvider.GetWave(index));
        }

        public void Reload()
        {
            // Reload = đọc lại nguồn VÀ chọn lại bậc (số người chơi có thể đã đổi).
            _baseProvider.Reload();
            _resolved = false;
            EnsureResolved();
        }

        private void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            int playerCount = 1;
            try
            {
                if (_playerCountProvider != null)
                {
                    playerCount = Mathf.Max(1, _playerCountProvider());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScaledWaveProvider] Không lấy được số người chơi, dùng 1. {e.Message}");
            }

            _tier = _config != null ? _config.Resolve(playerCount) : DifficultyConfigSO.Tier.Neutral;

            // Bộ level riêng cho bậc này (nếu có), ngược lại dùng nguồn gốc.
            _activeProvider = _tier.overrideCatalog != null
                ? new SoWaveProvider(_tier.overrideCatalog)
                : _baseProvider;

            Debug.Log($"[Difficulty] {playerCount} người chơi → bậc '{TierLabel}' " +
                      $"(catalog={(_tier.overrideCatalog != null ? _tier.overrideCatalog.name : "mặc định")}, " +
                      $"count×{_tier.enemyCountMultiplier}, interval×{_tier.spawnIntervalMultiplier}, " +
                      $"hp×{_tier.enemyHealthMultiplier}, build×{_tier.buildTimeMultiplier}) " +
                      $"— {_activeProvider.WaveCount} wave.");
        }

        private WaveData Scale(WaveData wave)
        {
            wave.buildPhaseDuration *= _tier.buildTimeMultiplier;

            if (wave.spawnGroups == null || wave.spawnGroups.Count == 0)
            {
                return wave;
            }

            bool countUnchanged = Mathf.Approximately(_tier.enemyCountMultiplier, 1f);
            bool intervalUnchanged = Mathf.Approximately(_tier.spawnIntervalMultiplier, 1f);
            if (countUnchanged && intervalUnchanged)
            {
                return wave;
            }

            var scaled = new List<SpawnGroup>(wave.spawnGroups.Count);
            foreach (SpawnGroup group in wave.spawnGroups)
            {
                SpawnGroup copy = group;

                // Luôn giữ tối thiểu 1 con — nhân xuống 0 sẽ làm wave trống, kẹt điều kiện hết quái.
                copy.count = Mathf.Max(1, Mathf.RoundToInt(group.count * _tier.enemyCountMultiplier));
                copy.spawnInterval = Mathf.Max(0f, group.spawnInterval * _tier.spawnIntervalMultiplier);

                scaled.Add(copy);
            }

            wave.spawnGroups = scaled;
            return wave;
        }
    }
}
