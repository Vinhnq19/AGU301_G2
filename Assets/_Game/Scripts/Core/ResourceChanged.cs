using DungeonBuilder.Core.Enums;

namespace DungeonBuilder.Core
{
    public readonly struct ResourceChanged
    {
        public ResourceType Type { get; }
        public int PreviousAmount { get; }
        public int CurrentAmount { get; }

        public ResourceChanged(ResourceType type, int previousAmount, int currentAmount)
        {
            Type = type;
            PreviousAmount = previousAmount;
            CurrentAmount = currentAmount;
        }
    }
}
