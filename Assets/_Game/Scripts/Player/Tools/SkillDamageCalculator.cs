using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Player.Tools
{
    /// <summary>
    /// Công thức sát thương lên resource dựa trên skill hiện tại.
    /// damage = 1 + (skill - 1) * skill
    ///   skill 0 / 1 → 1 (base hit)
    ///   skill 2 → 3
    ///   skill 3 → 7
    ///   skill 4 → 13
    ///   skill 5 → 21
    /// </summary>
    public static class SkillDamageCalculator
    {
        public const int BaseSkill = 1;
        public const int BaseDamage = 1;

        public static int Calculate(int skill)
        {
            int safeSkill = skill < BaseSkill ? BaseSkill : skill;
            return BaseDamage + (safeSkill - BaseSkill) * safeSkill;
        }

        /// <summary>
        /// Map tool category sang skill tương ứng:
        /// - Axe / Pickaxe → MiningSkill (khai thác tài nguyên thiên nhiên)
        /// - Weapon / Builder → ForgingSkill (rèn / chế tạo)
        /// - None / khác → MiningSkill (mặc định cho harvesting tool chưa biết).
        /// </summary>
        public static ResourceType SkillForTool(ToolType toolType)
        {
            return toolType switch
            {
                ToolType.Weapon  => ResourceType.ForgingSkill,
                ToolType.Builder => ResourceType.ForgingSkill,
                _                => ResourceType.MiningSkill
            };
        }
    }
}
