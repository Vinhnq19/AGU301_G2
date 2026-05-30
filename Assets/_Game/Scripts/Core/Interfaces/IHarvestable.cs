using DungeonBuilder.Player.Tools;

namespace DungeonBuilder.Core.Interfaces
{
    public interface IHarvestable : IInteractable
    {
        bool IsDepletable { get; }

        void TakeDamageFrom(ITool tool);
    }
}
