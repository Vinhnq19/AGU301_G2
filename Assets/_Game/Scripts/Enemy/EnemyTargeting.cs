using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonBuilder.Enemy
{
    /// <summary>Loại mục tiêu mà enemy có thể chọn để đánh.</summary>
    public enum EnemyTargetKind
    {
        Player = 0,
        Tower = 1,
        Core = 2
    }

    /// <summary>
    /// Một bậc ưu tiên: "tìm <see cref="kind"/> trong bán kính <see cref="detectRange"/>,
    /// và có rời đường đi để đuổi theo hay không (<see cref="chase"/>)".
    /// </summary>
    [Serializable]
    public struct TargetPriorityRule
    {
        [Tooltip("Loại mục tiêu của bậc ưu tiên này.")]
        public EnemyTargetKind kind;

        [Tooltip("Bán kính phát hiện mục tiêu. Để 0 = dùng attackRange của enemy " +
                 "(chỉ đánh khi đã ở trong tầm, không chủ động phát hiện từ xa).")]
        [Min(0f)] public float detectRange;

        [Tooltip("TRUE = rời waypoint để đuổi tới mục tiêu (giới hạn bởi leashRange). " +
                 "FALSE = chỉ đánh nếu mục tiêu tự vào tầm, vẫn đi tiếp về core.")]
        public bool chase;
    }

    /// <summary>
    /// Cấu hình "đánh gì trước" cho một loại enemy. Danh sách xếp theo thứ tự ưu tiên:
    /// phần tử đầu được xét trước, tìm thấy là chốt luôn — không xét bậc dưới.
    ///
    /// Ví dụ dùng:
    /// - Runner (lao thẳng core): [Core(0)] + [Player(0)] → chỉ đánh khi bị chắn đường.
    /// - Sapper (phá tháp): [Tower(6, chase=true)], [Player(0)], [Core(0)].
    /// - Hunter (săn người): [Player(7, chase=true)], [Core(0)].
    /// - Spitter (tầm xa, dọn tháp trước): [Tower(0)], [Player(0)], [Core(0)].
    /// </summary>
    [Serializable]
    public sealed class EnemyTargetingProfile
    {
        [Tooltip("Xếp theo THỨ TỰ ƯU TIÊN — trên xét trước. Bỏ trống = dùng mặc định " +
                 "(Player rồi Core, chỉ trong tầm đánh) giống hành vi cũ.")]
        public List<TargetPriorityRule> priorities = new();

        [Tooltip("Khi đuổi (chase), enemy được rời xa điểm trên đường đi tối đa bao nhiêu unit. " +
                 "Vượt quá thì bỏ mục tiêu và quay lại đường — chống bị kéo đi mãi (kiting).")]
        [Min(0f)] public float leashRange = 4f;

        /// <summary>Mặc định dùng khi designer chưa cấu hình: y hệt hành vi cũ (Player rồi Core, trong tầm đánh).</summary>
        private static readonly TargetPriorityRule[] Fallback =
        {
            new TargetPriorityRule { kind = EnemyTargetKind.Player, detectRange = 0f, chase = false },
            new TargetPriorityRule { kind = EnemyTargetKind.Core, detectRange = 0f, chase = false }
        };

        /// <summary>Danh sách bậc ưu tiên đang hiệu lực (tự fallback nếu chưa cấu hình).</summary>
        public IReadOnlyList<TargetPriorityRule> EffectiveRules =>
            priorities != null && priorities.Count > 0 ? (IReadOnlyList<TargetPriorityRule>)priorities : Fallback;

        /// <summary>Bán kính quét lớn nhất cần thiết — dùng cho MỘT lần OverlapCircle chung.</summary>
        public float GetMaxDetectRange(float attackRange)
        {
            float max = attackRange;
            IReadOnlyList<TargetPriorityRule> rules = EffectiveRules;
            for (int i = 0; i < rules.Count; i++)
            {
                float r = rules[i].detectRange <= 0f ? attackRange : rules[i].detectRange;
                if (r > max) max = r;
            }
            return max;
        }
    }
}
