using System;

namespace DungeonBuilder.UI.Base
{
    public interface IModel
    {
        event Action OnChanged;
    }
}
