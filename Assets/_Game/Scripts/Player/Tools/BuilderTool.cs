using DungeonBuilder.Building;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Player.Tools
{
    public sealed class BuilderTool : NetworkBehaviour, ITool
    {
        [SerializeField] private TowerType _selectedTowerType = TowerType.Arrow;
        [SerializeField] private BuildingController _buildingController;
        [SerializeField] private GridManager _gridManager;

        public ToolType ToolType => DungeonBuilder.Core.Enums.ToolType.Builder;

        private void Awake()
        {
            EnsureReferences();
        }

        public override void OnNetworkSpawn()
        {
            EnsureReferences();
        }

        [Inject]
        public void Construct(BuildingController buildingController, GridManager gridManager)
        {
            _buildingController = buildingController;
            _gridManager = gridManager;
        }

        public void UseAction(Vector3 targetPosition)
        {
            if (!IsOwner)
            {
                DBLog.Warning($"build.tool.not-owner.{NetworkObjectId}", $"Builder use ignored because this player is not owner. owner={OwnerClientId}.", 1f, this);
                return;
            }

            EnsureReferences();

            if (_buildingController == null)
            {
                DBLog.Warning($"build.tool.no-controller.{NetworkObjectId}", "Builder use ignored because BuildingController is missing.", 1f, this);
                return;
            }

            Vector2Int gridPosition = _gridManager != null
                ? _gridManager.WorldToGrid(targetPosition)
                : Vector2Int.RoundToInt(new Vector2(targetPosition.x, targetPosition.y));

            DBLog.Info($"build.tool.use.{NetworkObjectId}", $"Builder use accepted locally. target={targetPosition}, grid={gridPosition}, tower={_selectedTowerType}.", 0.2f, this);
            _buildingController.RequestPlaceTower(gridPosition, _selectedTowerType);
        }

        public void CancelAction()
        {
        }

        public void SetTowerType(TowerType towerType)
        {
            _selectedTowerType = towerType;
        }

        private void EnsureReferences()
        {
            if (_buildingController == null)
            {
                _buildingController = FindFirstObjectByType<BuildingController>();
            }

            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
            }
        }
    }
}
