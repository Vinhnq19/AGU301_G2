using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Player.Tools
{
    public sealed class PickaxeTool : HarvestToolBase
    {
        public override ToolType ToolType => DungeonBuilder.Core.Enums.ToolType.Pickaxe;
    }
}
