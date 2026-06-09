using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Networking;
using DungeonBuilder.Wave;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Building
{
    public sealed class BuildCommandValidator : IBuildCommandValidator
    {
        private readonly IGamePhaseProvider _phaseProvider;
        private readonly INetworkEntityResolver _entityResolver;
        private readonly GridManager _grid;
        private readonly BuildAuthoritySettingsSO _settings;

        public BuildCommandValidator(
            IGamePhaseProvider phaseProvider,
            INetworkEntityResolver entityResolver,
            GridManager grid,
            BuildAuthoritySettingsSO settings)
        {
            _phaseProvider = phaseProvider;
            _entityResolver = entityResolver;
            _grid = grid;
            _settings = settings;
        }

        public BuildValidationResult ValidatePlacement(ulong senderClientId, Vector2Int gridPosition)
        {
            if (_grid == null)
            {
                return new BuildValidationResult(BuildValidationCode.InvalidGridPosition);
            }

            BuildValidationResult commonResult = ValidateCommon(senderClientId, _grid.GridToWorld(gridPosition));
            if (!commonResult.IsAllowed)
            {
                return commonResult;
            }

            return _grid.IsValidPlacement(gridPosition)
                ? BuildValidationResult.Allowed
                : new BuildValidationResult(BuildValidationCode.InvalidGridPosition);
        }

        public BuildValidationResult ValidateTowerAction(
            ulong senderClientId,
            Vector2Int gridPosition,
            out NetworkObject towerObject)
        {
            towerObject = null;
            if (_phaseProvider == null || _phaseProvider.CurrentPhase != GamePhase.Build)
            {
                return new BuildValidationResult(BuildValidationCode.NotBuildPhase);
            }

            if (_grid == null
                || !_grid.TryGetCell(gridPosition, out GridCell cell)
                || _entityResolver == null
                || !_entityResolver.TryGetSpawnedObject(cell.TowerNetworkObjectId, out towerObject))
            {
                return new BuildValidationResult(BuildValidationCode.TowerNotFound);
            }

            return ValidateSenderDistance(senderClientId, towerObject.transform.position);
        }

        private BuildValidationResult ValidateCommon(ulong senderClientId, Vector3 targetPosition)
        {
            if (_phaseProvider == null || _phaseProvider.CurrentPhase != GamePhase.Build)
            {
                return new BuildValidationResult(BuildValidationCode.NotBuildPhase);
            }

            return ValidateSenderDistance(senderClientId, targetPosition);
        }

        private BuildValidationResult ValidateSenderDistance(ulong senderClientId, Vector3 targetPosition)
        {
            if (_entityResolver == null
                || !_entityResolver.TryGetPlayerObject(senderClientId, out NetworkObject playerObject))
            {
                return new BuildValidationResult(BuildValidationCode.SenderPlayerNotFound);
            }

            float maxDistance = _settings != null ? _settings.MaxBuildDistance : 6f;
            return Vector3.Distance(playerObject.transform.position, targetPosition) <= maxDistance
                ? BuildValidationResult.Allowed
                : new BuildValidationResult(BuildValidationCode.OutOfRange);
        }
    }
}
