using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Wave
{
    public interface IGamePhaseProvider
    {
        GamePhase CurrentPhase { get; }
    }
}
