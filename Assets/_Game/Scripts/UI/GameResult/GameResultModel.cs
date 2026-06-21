using System;
using DungeonBuilder.UI.Base;

namespace DungeonBuilder.UI.GameResult
{
    public sealed class GameResultModel : IModel
    {
        public event Action OnChanged;

        public bool IsVisible { get; private set; }
        public bool IsWin { get; private set; }

        public void Show(bool isWin)
        {
            if (IsVisible) return;
            IsVisible = true;
            IsWin = isWin;
            OnChanged?.Invoke();
        }
    }
}
