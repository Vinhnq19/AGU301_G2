using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Building
{
    public interface IBuildCommandValidator
    {
        BuildValidationResult ValidatePlacement(ulong senderClientId, Vector2Int gridPosition);

        BuildValidationResult ValidateTowerAction(
            ulong senderClientId,
            Vector2Int gridPosition,
            out NetworkObject towerObject);
    }
}
