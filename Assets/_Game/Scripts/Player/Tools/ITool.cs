using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Player.Tools
{
    public interface ITool
    {
        ToolType ToolType { get; }

        void UseAction(Vector3 targetPosition);

        void CancelAction();
    }
}
