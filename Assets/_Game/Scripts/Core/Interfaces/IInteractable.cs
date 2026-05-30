using DungeonBuilder.Player;

namespace DungeonBuilder.Core.Interfaces
{
    public interface IInteractable
    {
        void OnInteract(PlayerController interactor);
    }
}
