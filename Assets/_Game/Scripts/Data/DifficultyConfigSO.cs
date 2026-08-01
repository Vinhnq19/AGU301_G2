using System;
using System.Collections.Generic;
using DungeonBuilder.Data;
using UnityEngine;

namespace Assets._Game.Scripts.Data
{
    /// <summary>
    /// Cân bằng game theo SỐ NGƯỜI CHƠI vào trận.
    ///
    /// Mỗi bậc (tier) áp dụng khi số người chơi >= <see cref="Tier.minPlayers"/>; bậc có
    /// minPlayers lớn nhất mà vẫn <= số người thực tế sẽ được chọn.
    ///
    /// Hai cách dùng, có thể kết hợp:
    /// 1. <b>Bộ level riêng</b> — gán <see cref="Tier.overrideCatalog"/> để chơi hẳn một
    ///    WaveCatalog khác (thiết kế tay cho solo / co-op).
    /// 2. <b>Hệ số nhân</b> — giữ nguyên catalog gốc, chỉ nhân số quái / nhịp spawn / máu quái.
    ///    Nhanh, không phải dựng lại cả bộ wave.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Dungeon Builder/Data/Difficulty Config")]
    public sealed class DifficultyConfigSO : ScriptableObject
    {
        [Serializable]
        public struct Tier
        {
            [Tooltip("Nhãn cho dễ đọc trong Inspector, vd 'Solo', 'Co-op 2', '3+'.")]
            public string label;

            [Tooltip("Áp dụng khi số người chơi >= giá trị này.")]
            [Min(1)] public int minPlayers;

            [Tooltip("(Tùy chọn) Bộ level riêng cho mức này. Bỏ trống = dùng catalog mặc định của WaveManager.")]
            public WaveCatalogSO overrideCatalog;

            [Tooltip("Nhân số lượng quái mỗi nhóm. 1 = giữ nguyên, 0.6 = ít quái hơn (dễ).")]
            [Min(0.1f)] public float enemyCountMultiplier;

            [Tooltip("Nhân khoảng cách giữa 2 con spawn. >1 = quái ra thưa hơn (dễ thở), <1 = dồn dập.")]
            [Min(0.1f)] public float spawnIntervalMultiplier;

            [Tooltip("Nhân máu quái. 1 = giữ nguyên.")]
            [Min(0.1f)] public float enemyHealthMultiplier;

            [Tooltip("Nhân thời gian chuẩn bị (build phase). >1 = có thêm thời gian xây.")]
            [Min(0.1f)] public float buildTimeMultiplier;

            /// <summary>Bậc trung tính — không đổi gì so với dữ liệu gốc.</summary>
            public static Tier Neutral => new Tier
            {
                label = "Default",
                minPlayers = 1,
                overrideCatalog = null,
                enemyCountMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                enemyHealthMultiplier = 1f,
                buildTimeMultiplier = 1f
            };
        }

        [Tooltip("Các bậc độ khó. Không cần sắp xếp — hệ thống tự chọn bậc phù hợp nhất.")]
        [SerializeField] private List<Tier> _tiers = new();

        public IReadOnlyList<Tier> Tiers => _tiers;

        /// <summary>
        /// Chọn bậc phù hợp: bậc có minPlayers lớn nhất mà vẫn &lt;= <paramref name="playerCount"/>.
        /// Không cấu hình gì thì trả về bậc trung tính (giữ nguyên dữ liệu gốc).
        /// </summary>
        public Tier Resolve(int playerCount)
        {
            if (_tiers == null || _tiers.Count == 0)
            {
                return Tier.Neutral;
            }

            bool found = false;
            Tier best = Tier.Neutral;

            foreach (Tier tier in _tiers)
            {
                if (tier.minPlayers > playerCount)
                {
                    continue;
                }

                if (!found || tier.minPlayers > best.minPlayers)
                {
                    best = tier;
                    found = true;
                }
            }

            if (!found)
            {
                // Số người ít hơn mọi bậc đã khai báo → dùng bậc thấp nhất.
                best = _tiers[0];
                foreach (Tier tier in _tiers)
                {
                    if (tier.minPlayers < best.minPlayers)
                    {
                        best = tier;
                    }
                }
            }

            return Sanitize(best);
        }

        /// <summary>Chặn hệ số 0/âm do quên điền trong Inspector — sẽ làm hỏng cả wave.</summary>
        private static Tier Sanitize(Tier tier)
        {
            if (tier.enemyCountMultiplier <= 0f) tier.enemyCountMultiplier = 1f;
            if (tier.spawnIntervalMultiplier <= 0f) tier.spawnIntervalMultiplier = 1f;
            if (tier.enemyHealthMultiplier <= 0f) tier.enemyHealthMultiplier = 1f;
            if (tier.buildTimeMultiplier <= 0f) tier.buildTimeMultiplier = 1f;
            return tier;
        }
    }
}
